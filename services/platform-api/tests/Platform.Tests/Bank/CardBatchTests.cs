using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Platform.Bank.Payments;
using Platform.Bank.Registrations;
using Platform.Core.Abstractions;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Staff;
using Platform.Tests.Payroll;
using Platform.Tests.Support;

namespace Platform.Tests.Bank;

/// <summary>
/// Alta masiva files from the card queue (S10, SPEC §5.7, PLAN D85): the preview with the bank's rules, Banorte NOMINATIVA
/// with a sucursal and Banregio, both without card numbers; «Subido al banco»; «Tarjeta recibida» → principal account.
/// </summary>
public sealed class CardBatchTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _owner = null!;

    /// <summary>ECOVIA, one puesto and one employee per name, each with a card queued at <paramref name="bank"/>.</summary>
    private async Task<Dictionary<string, Guid>> ArrangeAsync(string bank, params string[] names)
    {
        _owner ??= await AsOwner();
        var client = await WithDb(db => db.AddClient("ECOVIA"));
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(client.Uid)))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions",
                PayrollGroupTests.NewPosition(validFrom: "2020-01-01"))))
            .GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        var employees = new Dictionary<string, Guid>();
        foreach (var name in names)
        {
            var id = await WithDb(async db =>
            {
                var person = await db.AddPerson(name, "PÉREZ", "LÓPEZ");
                var employee = new Employee { PersonId = person.Uid, PayrollGroupId = group, PositionId = position, Shift = "C/DÍA", HiredAt = new DateOnly(2026, 1, 1) };
                db.Add(employee);
                await db.SaveChangesAsync();
                return employee.Uid;
            });
            Assert.True((await _owner.PostAsJsonAsync($"/v1/employees/{id}/card-request", new { bank })).IsSuccessStatusCode);
            employees[name] = id;
        }
        return employees;
    }

    /// <summary>Everything the bank asks for (a CURP born 1 Jan 1990 in Nuevo León).</summary>
    private Task Complete(Guid employee, string curp = "PEPJ900101HNLRRN09", string rfc = "PEPJ900101AB1") => WithDb(async db =>
    {
        var personId = await db.Set<Employee>().Where(e => e.Uid == employee).Select(e => e.PersonId).SingleAsync();
        var p = await db.Set<Person>().SingleAsync(x => x.Uid == personId);
        (p.Curp, p.Rfc, p.Email, p.MobilePhone) = (curp, rfc, "juan.perez@example.com", "8112345670");
        (p.Street, p.ExtNumber, p.Neighborhood, p.PostalCode) = ("Av Constitución", "100", "Centro", "64000");
        (p.BirthDate, p.Gender, p.BirthState) = (new DateOnly(1990, 1, 1), Gender.Male, "Nuevo León");
        await db.SaveChangesAsync();
    });

    private async Task<IXLWorksheet> File(Guid batch, string sheet)
    {
        var key = await WithDb(db => db.Set<RegistrationBatch>().Where(b => b.Uid == batch).Select(b => b.StoragePath).SingleAsync());
        return new XLWorkbook(new MemoryStream(Factory.Storage.Contents[(StorageBucket.Exports, key)])).Worksheet(sheet);
    }

    [Fact]
    public async Task Banorte_goes_nominativa_with_a_sucursal_and_no_card_number_and_issues_block_the_file()
    {
        var e = await ArrangeAsync("banorte", "JUAN", "LUIS", "MARCO");
        await Complete(e["JUAN"]);
        // Marco added an account from the Candidates app meanwhile (that path leaves the request queued, D82): it is moot.
        await WithDb(async db =>
        {
            var personId = await db.Set<Employee>().Where(x => x.Uid == e["MARCO"]).Select(x => x.PersonId).SingleAsync();
            var bbva = await db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "012").Select(b => b.Uid).SingleAsync();
            db.Add(new PersonBankAccount
            {
                PersonId = personId,
                BankId = bbva,
                AccountType = BankAccountType.Clabe,
                AccountNumberEnc = [1],
                AccountLast4 = "0015",
                HolderName = "PÉREZ LÓPEZ MARCO",
                IsPrimary = true,
                ValidFrom = new DateOnly(2026, 1, 1),
            });
            await db.SaveChangesAsync();
        });

        var preview = await BodyOf(await _owner.GetAsync("/v1/card-batches/preview?bank=banorte"));
        Assert.Equal(["PÉREZ LÓPEZ MARCO"], preview.GetProperty("moot").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(RegistrationEndpoints.BranchRequired, preview.GetProperty("fileIssues").EnumerateArray().Select(x => x.GetString()));
        var luis = preview.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeeId").GetGuid() == e["LUIS"]);
        Assert.Contains("CURP", luis.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("field").GetString()));
        Assert.False(preview.GetProperty("canGenerate").GetBoolean());

        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.PostAsJsonAsync("/v1/card-batches", new { bank = "banorte", requestIds = (Guid[]?)null, branchToSend = "12345" })).StatusCode);
        var juanRequest = await WithDb(db => db.Set<CardRequest>().Where(r => r.EmployeeId == e["JUAN"]).Select(r => r.Uid).SingleAsync());
        var marcoRequest = await WithDb(db => db.Set<CardRequest>().Where(r => r.EmployeeId == e["MARCO"]).Select(r => r.Uid).SingleAsync());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _owner.PostAsJsonAsync("/v1/card-batches", new { bank = "banorte", requestIds = new[] { juanRequest }, branchToSend = (string?)null })).StatusCode);

        var response = await _owner.PostAsJsonAsync("/v1/card-batches", new { bank = "banorte", requestIds = new[] { juanRequest, marcoRequest }, branchToSend = "01234" });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var batch = (await BodyOf(response)).GetProperty("batch");
        Assert.Equal(("NOMINATIVA", "01234", 1), (batch.GetProperty("cardType").GetString(), batch.GetProperty("branchToSend").GetString(),
            batch.GetProperty("rows").GetArrayLength()));
        var number = batch.GetProperty("rows")[0].GetProperty("employeeNumber").GetString()!;
        Assert.Matches("^2[0-9]{9}$", number);

        var sheet = await File(batch.GetProperty("id").GetGuid(), "Masiva_xls");
        Assert.Equal((number, "NOMINATIVA", "01234", "PEPJ900101HNLRRN09"),
            (sheet.Cell("B3").GetString(), sheet.Cell("M3").GetString(), sheet.Cell("O3").GetString(), sheet.Cell("D3").GetString()));
        Assert.True(sheet.Cell("N3").IsEmpty());
        Assert.True(sheet.Cell("B4").IsEmpty());

        var statuses = await WithDb(db => db.Set<CardRequest>().ToDictionaryAsync(r => r.EmployeeId, r => r.Status));
        Assert.Equal((CardRequestStatus.InBatch, CardRequestStatus.Queued, CardRequestStatus.Cancelled), (statuses[e["JUAN"]], statuses[e["LUIS"]], statuses[e["MARCO"]]));
        Assert.Equal(number, await WithDb(db => db.Set<EmployeeBankNumber>().Where(n => n.EmployeeId == e["JUAN"]).Select(n => n.EmployeeNumber).SingleAsync()));
    }

    [Fact]
    public async Task Banregio_leaves_the_card_empty_and_a_received_card_becomes_the_principal_account()
    {
        var e = await ArrangeAsync("banregio", "JUAN");
        await Complete(e["JUAN"]);
        var generated = await BodyOf(await _owner.PostAsJsonAsync("/v1/card-batches", new { bank = "banregio", requestIds = (Guid[]?)null, branchToSend = (string?)null }));
        var batch = generated.GetProperty("batch");
        var batchId = batch.GetProperty("id").GetGuid();
        var request = batch.GetProperty("rows")[0].GetProperty("requestId").GetGuid();
        var sheet = await File(batchId, "Hoja1");
        Assert.Matches("^3[0-9]{9}$", sheet.Cell("A2").GetString());
        Assert.Equal(("01/01/1990", "MASCULINO", "NUEVO LEON"), (sheet.Cell("L2").GetString(), sheet.Cell("M2").GetString(), sheet.Cell("Q2").GetString()));
        Assert.True(sheet.Cell("P2").IsEmpty());

        // A card arrives only after the file went to the bank.
        Assert.Equal(RegistrationEndpoints.NotUploaded,
            await DetailOf(await _owner.PostAsJsonAsync($"/v1/card-requests/{request}/received", new { cardNumber = "4111111111111111" })));
        var uploaded = await BodyOf(await _owner.PostAsync($"/v1/card-batches/{batchId}/uploaded", null));
        Assert.Equal(("uploaded_to_bank", "uploaded"), (uploaded.GetProperty("status").GetString(), uploaded.GetProperty("rows")[0].GetProperty("status").GetString()));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await _owner.PostAsJsonAsync($"/v1/card-requests/{request}/received", new { cardNumber = "4111111111111112" })).StatusCode);
        var account = await BodyOf(await _owner.PostAsJsonAsync($"/v1/card-requests/{request}/received", new { cardNumber = "4111 1111 1111 1111" }));
        Assert.Equal(("debit_card", "1111", true, "058"), (account.GetProperty("accountType").GetString(), account.GetProperty("last4").GetString(),
            account.GetProperty("isPrimary").GetBoolean(), account.GetProperty("bankCode").GetString()));
        Assert.Equal(CardRequestStatus.Received, await WithDb(db => db.Set<CardRequest>().Where(r => r.Uid == request).Select(r => r.Status).SingleAsync()));
        var list = await BodyOf(await _owner.GetAsync("/v1/card-batches"));
        Assert.Equal("completed", list[0].GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await _owner.PostAsJsonAsync($"/v1/card-requests/{request}/received", new { cardNumber = "4111111111111111" })).StatusCode);
    }

    [Fact]
    public async Task Alta_files_need_the_card_grant_and_follow_the_client_scope()
    {
        await ArrangeAsync("banorte", "JUAN");
        var staffOnly = await As(UserRole.Administrator, PermissionModule.PayrollStaff);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffOnly.GetAsync("/v1/card-batches")).StatusCode);

        var other = await WithDb(db => db.AddClient("HEB"));
        var (_, scoped) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollCardRequests, ClientId = other.Uid });
        var preview = await BodyOf(await scoped.GetAsync("/v1/card-batches/preview?bank=banorte&branchToSend=01234"));
        Assert.Empty(preview.GetProperty("rows").EnumerateArray());
        Assert.Equal(RegistrationEndpoints.NothingQueued,
            await DetailOf(await scoped.PostAsJsonAsync("/v1/card-batches", new { bank = "banorte", requestIds = (Guid[]?)null, branchToSend = "01234" })));
    }

    [Fact]
    public async Task A_client_limited_grant_cannot_download_or_mark_a_batch_that_includes_other_clients()
    {
        var e = await ArrangeAsync("banorte", "JUAN", "LUIS");
        await Complete(e["JUAN"]);
        await Complete(e["LUIS"], "PEPL900101HNLRRN09", "PEPL900101AB2");
        // LUIS belongs to another client, so the one batch spans two.
        var heb = await WithDb(db => db.AddClient("HEB"));
        var hebGroup = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(heb.Uid)))).GetProperty("id").GetGuid();
        var hebPosition = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{hebGroup}/positions",
                PayrollGroupTests.NewPosition(validFrom: "2020-01-01"))))
            .GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        await WithDb(async db =>
        {
            var luis = await db.Set<Employee>().SingleAsync(x => x.Uid == e["LUIS"]);
            (luis.PayrollGroupId, luis.PositionId) = (hebGroup, hebPosition);
            await db.SaveChangesAsync();
        });
        var generated = await BodyOf(await _owner.PostAsJsonAsync("/v1/card-batches", new { bank = "banorte", requestIds = (Guid[]?)null, branchToSend = "01234" }));
        var batchId = generated.GetProperty("batch").GetProperty("id").GetGuid();

        var ecovia = await WithDb(db => db.Set<Platform.Core.Clients.Client>().Where(c => c.ShortName == "ECOVIA").Select(c => c.Uid).SingleAsync());
        var (_, scoped) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollCardRequests, ClientId = ecovia });
        // It is listed with only its own person...
        Assert.Single((await BodyOf(await scoped.GetAsync("/v1/card-batches")))[0].GetProperty("rows").EnumerateArray());
        // ...but the whole file and the status flip are not its to take.
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.PostAsync($"/v1/card-batches/{batchId}/download", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.PostAsync($"/v1/card-batches/{batchId}/uploaded", null)).StatusCode);
        Assert.Equal(BatchStatus.Generated, await WithDb(db => db.Set<RegistrationBatch>().Where(b => b.Uid == batchId).Select(b => b.Status).SingleAsync()));
        Assert.True((await _owner.PostAsync($"/v1/card-batches/{batchId}/download", null)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_batch_stays_with_the_client_it_was_generated_for_after_an_employee_changes_client()
    {
        var e = await ArrangeAsync("banorte", "JUAN", "LUIS");
        await Complete(e["JUAN"]);
        await Complete(e["LUIS"], "PEPL900101HNLRRN09", "PEPL900101AB2");
        var ecovia = await WithDb(db => db.Set<Platform.Core.Clients.Client>().Where(c => c.ShortName == "ECOVIA").Select(c => c.Uid).SingleAsync());
        var (_, scoped) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollCardRequests, ClientId = ecovia });
        var generated = await BodyOf(await scoped.PostAsJsonAsync("/v1/card-batches", new { bank = "banorte", requestIds = (Guid[]?)null, branchToSend = "01234" }));
        var batchId = generated.GetProperty("batch").GetProperty("id").GetGuid();

        // LUIS moves to another client's group after the file was made.
        var heb = await WithDb(db => db.AddClient("HEB"));
        var hebGroup = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(heb.Uid)))).GetProperty("id").GetGuid();
        await WithDb(async db =>
        {
            (await db.Set<Employee>().SingleAsync(x => x.Uid == e["LUIS"])).PayrollGroupId = hebGroup;
            await db.SaveChangesAsync();
        });

        Assert.Equal(2, (await BodyOf(await scoped.GetAsync("/v1/card-batches")))[0].GetProperty("rows").GetArrayLength());
        Assert.True((await scoped.PostAsync($"/v1/card-batches/{batchId}/download", null)).IsSuccessStatusCode);
        Assert.True((await scoped.PostAsync($"/v1/card-batches/{batchId}/uploaded", null)).IsSuccessStatusCode);
        // An admin limited to the new client never had this file and still does not see it.
        var (_, hebOnly) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollCardRequests, ClientId = heb.Uid });
        Assert.Empty((await BodyOf(await hebOnly.GetAsync("/v1/card-batches"))).EnumerateArray());
    }
}
