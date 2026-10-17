using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Platform.Bank.Payments;
using Platform.Core.Abstractions;
using Platform.Core.Audit;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;
using Platform.Payroll.Staff;
using Platform.Tests.Payroll;
using Platform.Tests.Support;

namespace Platform.Tests.Bank;

/// <summary>
/// Archivos de pago (S10, SPEC §5.5, PLAN D86) through the API: a week of five people — a CLABE, a debit card in a third
/// party's name, an Azteca account, nobody's account, and someone with nothing captured — paid by «Generar archivos de pago».
/// </summary>
public sealed class PaymentFilesTests(ApiFactory factory) : ApiTest(factory)
{
    private const string Clabe = "012180000000000015";
    private const string Card = "4111111111111111";
    private const string AztecaAccount = "12345678901234";
    private const string ChargeAccount = "01234567890123";

    private HttpClient _owner = null!;

    private sealed record Week(Guid Group, Guid Period, Dictionary<string, Guid> Employees, Dictionary<string, Guid> People);

    private static decimal Dec(JsonElement e) => e.ValueKind == JsonValueKind.Number
        ? e.GetDecimal()
        : decimal.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    /// <summary>
    /// ECOVIA, one puesto, Semana 38 (11–17 sep) with five people present every day but «Sin captura», then «Cerrar captura»;
    /// accounts: Ana a BBVA CLABE, Beto a Banorte card in his wife's name, Carla an Azteca account, Dora none.
    /// </summary>
    private async Task<Week> ArrangeAsync(bool withPayingAccount = true)
    {
        _owner ??= await AsOwner();
        var client = await WithDb(db => db.AddClient("ECOVIA"));
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(client.Uid)))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions",
                PayrollGroupTests.NewPosition(validFrom: "2020-01-01"))))
            .GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        var employees = new Dictionary<string, Guid>();
        var people = new Dictionary<string, Guid>();
        await WithDb(async db =>
        {
            foreach (var (name, captured) in new[] { ("ANA", true), ("BETO", true), ("CARLA", true), ("DORA", true), ("SIN CAPTURA", false) })
            {
                var person = await db.AddPerson(name, "PÉREZ", "LÓPEZ");
                var employee = new Employee { PersonId = person.Uid, PayrollGroupId = group, PositionId = position, Shift = "C/DÍA", HiredAt = new DateOnly(2026, 1, 1) };
                db.Add(employee);
                await db.SaveChangesAsync();
                if (captured)
                    for (var d = new DateOnly(2026, 9, 11); d <= new DateOnly(2026, 9, 17); d = d.AddDays(1))
                        db.Add(new AttendanceRecord
                        {
                            EmployeeId = employee.Uid,
                            WorkDate = d,
                            Status = d == new DateOnly(2026, 9, 13) ? AttendanceStatus.RestDay : AttendanceStatus.Present,
                            HoursWorked = 12,
                            IsLocked = true,
                            RecordedAt = DateTimeOffset.UnixEpoch,
                        });
                await db.SaveChangesAsync();
                employees[name] = employee.Uid;
                people[name] = person.Uid;
            }
        });
        var period = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/periods", new { date = "2026-09-14" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await _owner.PostAsync($"/v1/payroll/periods/{period}/close-capture", null)).StatusCode);

        await AddAccount(employees["ANA"], "012", "clabe", Clabe);
        await AddAccount(employees["BETO"], "072", "debit_card", Card, "María López Garza");
        await AddAccount(employees["CARLA"], "127", "own_account", AztecaAccount);
        if (withPayingAccount)
            Assert.Equal(HttpStatusCode.OK, (await _owner.PutAsJsonAsync("/v1/payroll/paying-account", PayingAccount())).StatusCode);
        return new Week(group, period, employees, people);
    }

    private static object PayingAccount(string? number = ChargeAccount) => new
    {
        name = "Cuenta Azteca — MAXIMA",
        chargeAccountNumber = number,
        holderName = "MAXIMA REPARACION Y LIMPIEZA SA DE CV",
        holderRfc = "MRL2407307G7",
        chargeLegend = (string?)null,
        creditLegend = (string?)null,
    };

    private async Task AddAccount(Guid employee, string bankCode, string type, string number, string? holder = null)
    {
        var bank = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == bankCode).Select(b => b.Uid).SingleAsync());
        var response = await _owner.PostAsJsonAsync($"/v1/employees/{employee}/bank-accounts", new
        {
            bankId = bank,
            accountType = type,
            accountNumber = number,
            holderName = holder,
            alias = (string?)null,
            validFrom = (string?)null,
            makePrimary = true,
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> Panel(Guid period) => await BodyOf(await _owner.GetAsync($"/v1/payroll/periods/{period}/payments"));

    /// <summary>The line of the person with these given names («PÉREZ LÓPEZ ANA»).</summary>
    private static JsonElement Line(JsonElement panel, string firstNames) =>
        panel.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("fullName").GetString()!.EndsWith(" " + firstNames, StringComparison.Ordinal));

    private async Task<IXLWorksheet> File(string key) =>
        new XLWorkbook(new MemoryStream(Factory.Storage.Contents[(StorageBucket.Exports, key)])).Worksheet(1);

    [Fact]
    public async Task Generar_pays_the_lines_with_an_account_freezes_the_period_and_writes_the_azteca_file()
    {
        var w = await ArrangeAsync();
        var panel = await Panel(w.Period);
        Assert.Equal(("ready", "ready", "ready", "missing_account", "nothing_due"),
            (Line(panel, "ANA").GetProperty("state").GetString(), Line(panel, "BETO").GetProperty("state").GetString(),
                Line(panel, "CARLA").GetProperty("state").GetString(), Line(panel, "DORA").GetProperty("state").GetString(),
                Line(panel, "SIN CAPTURA").GetProperty("state").GetString()));
        Assert.Equal("MARÍA LÓPEZ GARZA", Line(panel, "BETO").GetProperty("thirdPartyHolder").GetString());
        Assert.Equal((3, 3, true), (panel.GetProperty("payable").GetInt32(), panel.GetProperty("frequentAccounts").GetInt32(),
            panel.GetProperty("canGenerate").GetBoolean()));
        var owed = Dec(panel.GetProperty("totalNet"));
        var payable = Dec(panel.GetProperty("payableAmount"));
        Assert.True(payable < owed);

        var response = await _owner.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generated = await BodyOf(response);
        var batch = generated.GetProperty("batch");
        Assert.Equal(("period_payment", 3, payable), (batch.GetProperty("kind").GetString(), batch.GetProperty("rowCount").GetInt32(),
            Dec(batch.GetProperty("totalAmount"))));
        Assert.Equal(JsonValueKind.Object, generated.GetProperty("frequentAccounts").ValueKind);
        Assert.Empty(generated.GetProperty("skipped").EnumerateArray());

        // The bank's layout: headers in row 1, one row per payment, importe in E (a number), codes derived.
        var stored = await WithDb(db => db.Set<PaymentBatch>().SingleAsync());
        var sheet = await File(stored.StoragePath);
        Assert.Equal("NO. DE CUENTA CARGO", sheet.Cell("A1").GetString());
        Assert.True(sheet.Cell("E1").IsEmpty());
        var rows = Enumerable.Range(2, 3).Select(r => new
        {
            Charge = sheet.Cell($"A{r}").GetString(),
            Amount = (decimal)sheet.Cell($"E{r}").GetDouble(),
            Bank = sheet.Cell($"G{r}").GetString(),
            Type = sheet.Cell($"H{r}").GetString(),
            Account = sheet.Cell($"I{r}").GetString(),
            Name = sheet.Cell($"J{r}").GetString(),
            PaymentType = sheet.Cell($"O{r}").GetString(),
        }).ToList();
        Assert.True(sheet.Cell("A5").IsEmpty());
        Assert.All(rows, r => Assert.Equal(ChargeAccount, r.Charge));
        Assert.Equal(payable, rows.Sum(r => r.Amount));
        Assert.Equal(["012|40|1", "072|3|1", "127|1|3"], rows.Select(r => $"{r.Bank}|{r.Type}|{r.PaymentType}").Order());
        Assert.Contains(rows, r => r.Account == Card && r.Name == "MARÍA LÓPEZ GARZA");

        // Every account went to the cuentas frecuentes file, with its type code in C and J (SPEC D33).
        var frequent = await File(stored.FrequentAccountsStoragePath!);
        Assert.Equal(["01", "03", "40"], Enumerable.Range(1, 3).Select(r => frequent.Cell($"C{r}").GetString()).Order());
        Assert.All(Enumerable.Range(1, 3), r => Assert.Equal("ecovia", frequent.Cell($"A{r}").GetString()));

        // The period is Pagada with its one total (what is owed), and the grid shows where each line went.
        var period = await WithDb(db => db.Set<PayrollPeriod>().SingleAsync(p => p.Uid == w.Period));
        Assert.Equal((PeriodStatus.Paid, owed), (period.Status, period.TotalNet));
        var grid = await BodyOf(await _owner.GetAsync($"/v1/payroll/periods/{w.Period}"));
        Assert.False(grid.GetProperty("editable").GetBoolean());
        Assert.Equal(owed, Dec(grid.GetProperty("totalNet")));
        var states = grid.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("fullName").GetString()!.Split(' ')[^1], r => r.GetProperty("payment").GetProperty("state").GetString());
        Assert.Equal(("pending", "missing_account", "nothing_due"), (states["ANA"], states["DORA"], states["CAPTURA"]));

        // Each number read in clear is in data_access_log.
        var decrypted = await WithDb(db => db.Set<DataAccessEntry>().Where(e => e.Access == DataAccessKind.DecryptAccount).Select(e => e.PersonId).ToListAsync());
        Assert.Equal(new[] { w.People["ANA"], w.People["BETO"], w.People["CARLA"] }.Order(), decrypted.Order());

        // Nothing else to pay until Dora has an account; the paid lines refuse any change but their payment columns.
        Assert.Equal(PaymentFiles.NothingPending, await DetailOf(await _owner.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null)));
        var ana = Line(panel, "ANA").GetProperty("lineId").GetGuid();
        await Assert.ThrowsAnyAsync<Exception>(() => WithDb(db =>
            db.Set<PrepayrollLine>().Where(l => l.Uid == ana).ExecuteUpdateAsync(s => s.SetProperty(l => l.Comments, "cambio"))));
        await WithDb(db => db.Set<PrepayrollLine>().Where(l => l.Uid == ana)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.PaymentStatus, PaymentRowStatus.Paid)));
    }

    [Fact]
    public async Task Subido_al_banco_pays_the_rows_and_a_rejected_one_goes_into_the_pending_file_with_the_fixed_accounts()
    {
        var w = await ArrangeAsync();
        var batch = (await BodyOf(await _owner.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null))).GetProperty("batch");
        var batchId = batch.GetProperty("id").GetGuid();
        var anaRow = batch.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("fullName").GetString()!.EndsWith(" ANA", StringComparison.Ordinal))
            .GetProperty("id").GetGuid();
        Assert.Equal(PaymentEndpoints.NotUploaded,
            await DetailOf(await _owner.PostAsJsonAsync($"/v1/payroll/payment-batch-rows/{anaRow}/reject", new { reason = "Cuenta cancelada" })));

        var uploaded = await BodyOf(await _owner.PostAsync($"/v1/payroll/payment-batches/{batchId}/uploaded", null));
        Assert.Equal("uploaded_to_bank", uploaded.GetProperty("status").GetString());
        Assert.All(uploaded.GetProperty("rows").EnumerateArray(), r => Assert.Equal("paid", r.GetProperty("status").GetString()));
        Assert.True(await WithDb(db => db.Set<PersonBankAccount>().AllAsync(a => a.IsFrequentAccountRegistered)));
        Assert.Equal(HttpStatusCode.Conflict, (await _owner.PostAsync($"/v1/payroll/payment-batches/{batchId}/uploaded", null)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await _owner.PostAsJsonAsync($"/v1/payroll/payment-batch-rows/{anaRow}/reject", new { reason = " " })).StatusCode);
        var rejected = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/payment-batch-rows/{anaRow}/reject", new { reason = "Cuenta cancelada" }));
        Assert.Equal("rejected", rejected.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == anaRow).GetProperty("status").GetString());

        // Ana gives a new account (a debit card), Dora her first one: the pending file pays both, and only Dora's new account and
        // Ana's new card need registering.
        await AddAccount(w.Employees["ANA"], "012", "debit_card", "5555555555554444");
        await AddAccount(w.Employees["DORA"], "012", "clabe", Clabe);
        var panel = await Panel(w.Period);
        Assert.Equal(("rejected", "ready", 2), (Line(panel, "ANA").GetProperty("state").GetString(),
            Line(panel, "DORA").GetProperty("state").GetString(), panel.GetProperty("payable").GetInt32()));
        var grid = await BodyOf(await _owner.GetAsync($"/v1/payroll/periods/{w.Period}"));
        var dora = grid.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeeId").GetGuid() == w.Employees["DORA"]).GetProperty("payment");
        Assert.Equal(("ready", "0015"), (dora.GetProperty("state").GetString(), dora.GetProperty("last4").GetString()));
        var pending = await BodyOf(await _owner.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null));
        var second = pending.GetProperty("batch");
        Assert.Equal(("pending_payment", 2, 2), (second.GetProperty("kind").GetString(), second.GetProperty("rowCount").GetInt32(),
            second.GetProperty("frequentAccountsCount").GetInt32()));
        var anaLine = await WithDb(db => db.Set<PrepayrollLine>().SingleAsync(l => l.EmployeeId == w.Employees["ANA"]));
        Assert.Equal((PaymentRowStatus.Pending, "4444"), (anaLine.PaymentStatus, anaLine.AccountLast4));

        // The deposit date, never before the period.
        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.PutAsJsonAsync($"/v1/payroll/periods/{w.Period}/deposit-date", new { date = "2026-09-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _owner.PutAsJsonAsync($"/v1/payroll/periods/{w.Period}/deposit-date", new { date = "2026-09-18" })).StatusCode);
        Assert.Equal("2026-09-18", (await Panel(w.Period)).GetProperty("depositDate").GetString());

        // Each file downloads again (audited as an export).
        var link = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/payment-batches/{batchId}/download", new { file = "frequent_accounts" }));
        Assert.StartsWith("CUENTAS FRECUENTES SEM 38 ECOVIA", link.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Without_a_review_or_a_paying_account_nothing_is_generated_and_two_clicks_make_one_file()
    {
        var w = await ArrangeAsync(withPayingAccount: false);
        Assert.Equal(HttpStatusCode.NoContent, (await _owner.GetAsync("/v1/payroll/paying-account")).StatusCode);
        var panel = await Panel(w.Period);
        Assert.Equal((false, PaymentFiles.NoPayingAccount), (panel.GetProperty("canGenerate").GetBoolean(), panel.GetProperty("cannotGenerate").GetString()));
        Assert.Equal(PaymentFiles.NoPayingAccount, await DetailOf(await _owner.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null)));

        // A new paying account needs its number; on an edit, an empty number keeps the stored one.
        Assert.Equal(HttpStatusCode.BadRequest, (await _owner.PutAsJsonAsync("/v1/payroll/paying-account", PayingAccount(null))).StatusCode);
        var account = await BodyOf(await _owner.PutAsJsonAsync("/v1/payroll/paying-account", PayingAccount()));
        Assert.Equal("0123", account.GetProperty("last4").GetString());
        Assert.Equal("0123", (await BodyOf(await _owner.PutAsJsonAsync("/v1/payroll/paying-account", PayingAccount(null)))).GetProperty("last4").GetString());

        var next = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{w.Group}/periods", new { date = "2026-09-21" }))).GetProperty("id").GetGuid();
        Assert.Equal(PaymentFiles.NotReviewed, await DetailOf(await _owner.PostAsync($"/v1/payroll/periods/{next}/payment-files", null)));

        var clicks = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => _owner.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null)));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], clicks.Select(c => c.StatusCode).Order());
        Assert.Equal(1, await WithDb(db => db.Set<PaymentBatch>().CountAsync()));
    }

    [Fact]
    public async Task A_paid_periods_days_no_longer_change()
    {
        var w = await ArrangeAsync();
        await _owner.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null);
        var unlock = await _owner.PostAsJsonAsync("/v1/attendance/unlock", new
        {
            employeeId = w.Employees["DORA"],
            date = "2026-09-15",
            siteId = (Guid?)null,
            status = "absent",
            hoursWorked = (string?)null,
            overtimeHours = (string?)null,
            note = (string?)null,
            reason = "No vino",
        });
        Assert.Equal(AttendanceRules.PaidPeriod, await DetailOf(unlock));

        // Vacaciones from the 16th: the paid days stay as paid, the next week's are written.
        var vacation = await _owner.PostAsJsonAsync($"/v1/employees/{w.Employees["DORA"]}/status",
            new { status = "vacation", startDate = "2026-09-16", endDate = "2026-09-20", reason = (string?)null, reference = (string?)null });
        Assert.True(vacation.IsSuccessStatusCode, await vacation.Content.ReadAsStringAsync());
        var days = await WithDb(db => db.Set<AttendanceRecord>().Where(a => a.EmployeeId == w.Employees["DORA"] && a.WorkDate >= new DateOnly(2026, 9, 16))
            .OrderBy(a => a.WorkDate).Select(a => a.Status).ToListAsync());
        Assert.Equal([AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Vacation, AttendanceStatus.Vacation, AttendanceStatus.Vacation], days);
    }

    [Fact]
    public async Task Payment_files_need_their_grant_and_follow_the_client_scope()
    {
        var w = await ArrangeAsync();
        var prepayrollOnly = await As(UserRole.Administrator, PermissionModule.PayrollPrepayroll);
        Assert.Equal(HttpStatusCode.Forbidden, (await prepayrollOnly.GetAsync($"/v1/payroll/periods/{w.Period}/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await prepayrollOnly.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null)).StatusCode);

        var other = await WithDb(db => db.AddClient("HEB"));
        var (_, scoped) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollPaymentFiles, ClientId = other.Uid });
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.GetAsync($"/v1/payroll/periods/{w.Period}/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scoped.PostAsync($"/v1/payroll/periods/{w.Period}/payment-files", null)).StatusCode);
        Assert.Equal(PaymentEndpoints.PayingAccountNeedsAllClients,
            await DetailOf(await scoped.PutAsJsonAsync("/v1/payroll/paying-account", PayingAccount())));
    }
}
