using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Audit;
using Platform.Core.Catalogs;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Staff;
using Platform.Recruiting.Candidates;
using Platform.Tests.Recruiting;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>Personal (SPEC §5.1, §5.5, §5.8; DATA_MODEL §5.4–5.5b; PLAN D82).</summary>
public sealed class EmployeeTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _owner = null!;
    private Guid _ecovia, _group, _validador, _supervisor;
    private DateOnly _today;

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task Arrange()
    {
        _today = new SystemClock().Today();
        _ecovia = (await WithDb(db => db.AddClient("ECOVIA"))).Uid;
        _owner = await AsOwner();
        _group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(_ecovia)))).GetProperty("id").GetGuid();
        await _owner.PostAsJsonAsync($"/v1/payroll/groups/{_group}/positions", PayrollGroupTests.NewPosition(validFrom: Day(_today.AddYears(-1))));
        var detail = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{_group}/positions",
            PayrollGroupTests.NewPosition("Supervisor", "585.71", Day(_today.AddYears(-1)))));
        var positions = detail.GetProperty("positions").EnumerateArray().ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("id").GetGuid());
        (_validador, _supervisor) = (positions["VALIDADOR"], positions["SUPERVISOR"]);
    }

    private object Alta(Guid personId, string? cardBank = "banorte", Guid? candidateId = null, int hiredDaysAgo = 100) => new
    {
        personId,
        payrollGroupId = _group,
        positionId = _validador,
        shift = "c/día",
        hiredAt = candidateId is null ? Day(_today.AddDays(-hiredDaysAgo)) : null,
        dailySalaryPocketOverride = (string?)null,
        dailySalaryImssOverride = (string?)null,
        hourlyRateOverride = (string?)null,
        daysWorkedBonusOverride = (string?)null,
        notes = (string?)null,
        cardBank,
        candidateId,
    };

    private async Task<Guid> Hire(Guid personId, string? cardBank = "banorte")
    {
        var response = await _owner.PostAsJsonAsync("/v1/employees", Alta(personId, cardBank));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await BodyOf(response)).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> Status(Guid employee, string status, DateOnly start, DateOnly? end, string? reason = null) =>
        _owner.PostAsJsonAsync($"/v1/employees/{employee}/status",
            new { status, startDate = Day(start), endDate = end is { } e ? Day(e) : null, reason, reference = (string?)null });

    private Task<List<AttendanceRecord>> Days(Guid employee) =>
        WithDb(db => db.Set<AttendanceRecord>().Where(a => a.EmployeeId == employee).OrderBy(a => a.WorkDate).ToListAsync());

    [Fact]
    public async Task An_alta_without_an_account_is_queued_for_a_card_and_one_employment_per_group()
    {
        await Arrange();
        var ana = await WithDb(db => db.AddPerson());

        var noBank = await _owner.PostAsJsonAsync("/v1/employees", Alta(ana.Uid, cardBank: null));
        Assert.Equal(EmployeeEndpoints.CardBankRequired, (await ErrorsOf(noBank))["cardBank"][0]);

        var created = await BodyOf(await _owner.PostAsJsonAsync("/v1/employees", Alta(ana.Uid)));
        Assert.Equal("C/DÍA", created.GetProperty("shift").GetString());
        Assert.Equal("queued", created.GetProperty("cardRequest").GetProperty("status").GetString());
        Assert.Equal("banorte", created.GetProperty("cardRequest").GetProperty("bank").GetString());
        Assert.Equal("428.57", created.GetProperty("rates").GetProperty("dailySalaryPocket").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await _owner.PostAsJsonAsync("/v1/employees", Alta(ana.Uid))).StatusCode);
        var row = (await BodyOf(await _owner.GetAsync("/v1/employees"))).EnumerateArray().Single();
        Assert.False(row.GetProperty("hasAccount").GetBoolean());
        Assert.Equal("VALIDADOR", row.GetProperty("positionName").GetString());
        var queue = await BodyOf(await _owner.GetAsync("/v1/card-requests"));
        Assert.Single(queue.EnumerateArray());
    }

    [Fact]
    public async Task A_primary_account_makes_the_card_request_unnecessary()
    {
        await Arrange();
        var ana = await WithDb(db => db.AddPerson());
        var employee = await Hire(ana.Uid);
        var bbva = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "012").Select(b => b.Uid).SingleAsync());

        var wrongBank = await _owner.PostAsJsonAsync($"/v1/employees/{employee}/bank-accounts",
            new { bankId = bbva, accountType = "clabe", accountNumber = "072580015000000019", holderName = (string?)null, alias = (string?)null, validFrom = (string?)null, makePrimary = true });
        Assert.Contains("otro banco", (await ErrorsOf(wrongBank))["accountNumber"][0]);

        var accounts = await BodyOf(await _owner.PostAsJsonAsync($"/v1/employees/{employee}/bank-accounts",
            new { bankId = bbva, accountType = "clabe", accountNumber = "012580015000000011", holderName = "María López Garza", alias = "Cuenta de mi esposa", validFrom = (string?)null, makePrimary = true }));
        var account = accounts.EnumerateArray().Single();
        Assert.True(account.GetProperty("isPrimary").GetBoolean());
        Assert.True(account.GetProperty("isThirdParty").GetBoolean());
        Assert.Equal("MARÍA LÓPEZ GARZA", account.GetProperty("holderName").GetString());

        var detail = await BodyOf(await _owner.GetAsync($"/v1/employees/{employee}"));
        Assert.True(detail.GetProperty("hasAccount").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("cardRequest").ValueKind);
        Assert.Equal(CardRequestStatus.Cancelled, await WithDb(db => db.Set<CardRequest>().Select(r => r.Status).SingleAsync()));

        // Closing the account takes away the principal; a closed account cannot be made principal again.
        var accountId = account.GetProperty("id").GetGuid();
        await _owner.PutAsJsonAsync($"/v1/employees/{employee}/bank-accounts/{accountId}",
            new { holderName = "María López Garza", alias = "Cuenta de mi esposa", validTo = Day(_today) });
        Assert.Equal(HttpStatusCode.Conflict, (await _owner.PostAsync($"/v1/employees/{employee}/bank-accounts/{accountId}/primary", null)).StatusCode);
    }

    [Fact]
    public async Task An_account_typed_at_alta_is_the_principal_and_no_card_is_requested()
    {
        await Arrange();
        var ana = await WithDb(db => db.AddPerson());
        var azteca = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "127").Select(b => b.Uid).SingleAsync());
        var body = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Alta(ana.Uid, cardBank: null)))!.AsObject();
        body["bankAccount"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new
        {
            bankId = azteca,
            accountType = "debit_card",
            accountNumber = "4152 3138 0000 1238",
            holderName = (string?)null,
            alias = (string?)null,
            validFrom = (string?)null,
            makePrimary = false,
        }));
        Assert.Contains("bankAccount.accountNumber", (await ErrorsOf(await _owner.PostAsJsonAsync("/v1/employees", body))).Keys);
        Assert.False(await WithDb(db => db.Set<Employee>().AnyAsync()));

        body["bankAccount"]!["accountNumber"] = "4152 3138 0000 1237";
        var created = await BodyOf(await _owner.PostAsJsonAsync("/v1/employees", body));
        Assert.True(created.GetProperty("hasAccount").GetBoolean());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("cardRequest").ValueKind);
        Assert.False(await WithDb(db => db.Set<CardRequest>().AnyAsync()));
    }

    [Fact]
    public async Task Vacaciones_fill_attendance_locked_and_reactivar_ends_them_yesterday()
    {
        await Arrange();
        var employee = await Hire((await WithDb(db => db.AddPerson())).Uid);

        var vacation = await BodyOf(await Status(employee, "vacation", _today.AddDays(-2), _today.AddDays(4)));
        Assert.Equal("vacation", vacation.GetProperty("status").GetString());
        Assert.Equal(Day(_today.AddDays(4)), vacation.GetProperty("period").GetProperty("endDate").GetString());
        var days = await Days(employee);
        Assert.Equal(7, days.Count);
        Assert.All(days, d => Assert.True(d.IsLocked && d.Status == AttendanceStatus.Vacation));

        var overlap = await Status(employee, "sick_leave", _today.AddDays(3), _today.AddDays(10));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.StartsWith("Se cruza con Vacaciones", await DetailOf(overlap));

        var periodId = vacation.GetProperty("period").GetProperty("id").GetGuid();
        var back = await BodyOf(await _owner.PostAsync($"/v1/employees/{employee}/status/{periodId}/end", null));
        Assert.Equal("active", back.GetProperty("status").GetString());
        Assert.Equal(2, (await Days(employee)).Count); // the two days already taken stay
        Assert.Equal(EmployeeStatus.Active, await WithDb(db => db.Set<Employee>().Select(e => e.Status).SingleAsync()));

        // The freed days take another period.
        Assert.Equal(HttpStatusCode.OK, (await Status(employee, "sick_leave", _today.AddDays(3), _today.AddDays(10))).StatusCode);
    }

    [Fact]
    public async Task An_incapacidad_end_moves_and_the_attendance_follows()
    {
        await Arrange();
        var employee = await Hire((await WithDb(db => db.AddPerson())).Uid);
        var sick = await BodyOf(await Status(employee, "sick_leave", _today, _today.AddDays(9)));
        var periodId = sick.GetProperty("period").GetProperty("id").GetGuid();

        await _owner.PutAsJsonAsync($"/v1/employees/{employee}/status/{periodId}", new { endDate = Day(_today.AddDays(4)), reference = "st-7 448120" });
        Assert.Equal(5, (await Days(employee)).Count);
        var longer = await BodyOf(await _owner.PutAsJsonAsync($"/v1/employees/{employee}/status/{periodId}", new { endDate = Day(_today.AddDays(14)), reference = (string?)null }));
        Assert.Equal(15, (await Days(employee)).Count);
        Assert.Equal("ST-7 448120", longer.GetProperty("period").GetProperty("reference").GetString());
        Assert.All(await Days(employee), d => Assert.Equal(AttendanceStatus.SickLeave, d.Status));

        // A future suspension can be cancelled outright; a suspension needs a motivo.
        Assert.Contains("reason", (await ErrorsOf(await Status(employee, "suspended", _today.AddDays(20), _today.AddDays(22)))).Keys);
        var suspension = await BodyOf(await Status(employee, "suspended", _today.AddDays(20), _today.AddDays(22), "Falta de uniforme"));
        Assert.Equal("suspended", suspension.GetProperty("nextPeriod").GetProperty("status").GetString());
        var future = suspension.GetProperty("nextPeriod").GetProperty("id").GetGuid();
        await _owner.PostAsync($"/v1/employees/{employee}/status/{future}/end", null);
        Assert.False(await WithDb(db => db.Set<EmployeeStatusPeriod>().AnyAsync(p => p.Uid == future)));
        Assert.Equal(15, (await Days(employee)).Count);
    }

    [Fact]
    public async Task A_baja_needs_a_catalog_motivo_cancels_the_card_and_reingreso_is_a_new_employment()
    {
        await Arrange();
        var person = (await WithDb(db => db.AddPerson())).Uid;
        var employee = await Hire(person);

        Assert.Contains("reason", (await ErrorsOf(await Status(employee, "terminated", _today, null, "Se fue"))).Keys);
        var baja = await BodyOf(await Status(employee, "terminated", _today, null, "Renuncia voluntaria"));
        Assert.Equal("terminated", baja.GetProperty("status").GetString());
        Assert.Equal(Day(_today), baja.GetProperty("terminatedAt").GetString());
        Assert.Empty(await Days(employee));
        Assert.Equal(CardRequestStatus.Cancelled, await WithDb(db => db.Set<CardRequest>().Select(r => r.Status).SingleAsync()));

        Assert.Empty((await BodyOf(await _owner.GetAsync("/v1/employees"))).EnumerateArray());
        Assert.Single((await BodyOf(await _owner.GetAsync("/v1/employees?status=terminated"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await Status(employee, "vacation", _today.AddDays(1), _today.AddDays(2))).StatusCode);
        var periodId = baja.GetProperty("period").GetProperty("id").GetGuid();
        Assert.Equal(StaffRules.BajaIsReingreso, await DetailOf(await _owner.PostAsync($"/v1/employees/{employee}/status/{periodId}/end", null)));

        var rehire = await _owner.PostAsJsonAsync($"/v1/employees/{employee}/rehire",
            new { payrollGroupId = _group, positionId = _supervisor, shift = "C/NOCHE", hiredAt = Day(_today.AddDays(1)), cardBank = "banregio" });
        var again = await BodyOf(rehire);
        Assert.NotEqual(employee, again.GetProperty("id").GetGuid());
        Assert.Equal("SUPERVISOR", again.GetProperty("positionName").GetString());
        Assert.Equal("banregio", again.GetProperty("cardRequest").GetProperty("bank").GetString());
        Assert.Equal(2, await WithDb(db => db.Set<Employee>().CountAsync(e => e.PersonId == person)));
    }

    [Fact]
    public async Task Convertir_en_empleado_takes_the_hire_date_and_the_vacantes_bank_once()
    {
        await Arrange();
        var banorte = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "072").Select(b => b.Uid).SingleAsync());
        var opening = OpeningTests.NewOpening(_ecovia, position: "Validador");
        var json = JsonSerializer.SerializeToNode(opening)!.AsObject();
        json["paymentBankIds"] = new System.Text.Json.Nodes.JsonArray(banorte.ToString());
        var openingId = (await BodyOf(await _owner.PostAsJsonAsync("/v1/openings", json))).GetProperty("id").GetGuid();
        var candidate = await BodyOf(await _owner.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(openingId,
            new { firstNames = "Rosa", lastNamePaternal = "Leal", lastNameMaternal = "Garza", isForeign = false })));
        var candidateId = candidate.GetProperty("id").GetGuid();
        var personId = candidate.GetProperty("person").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await _owner.GetAsync($"/v1/employees/conversion/{candidateId}")).StatusCode);
        var hired = await _owner.PostAsJsonAsync($"/v1/candidates/{candidateId}/stage", new { stage = "hired", reason = (string?)null, hiredAt = Day(_today) });
        Assert.True(hired.IsSuccessStatusCode, await hired.Content.ReadAsStringAsync());

        var conversionResponse = await _owner.GetAsync($"/v1/employees/conversion/{candidateId}");
        Assert.True(conversionResponse.IsSuccessStatusCode, await conversionResponse.Content.ReadAsStringAsync());
        var conversion = await BodyOf(conversionResponse);
        Assert.Equal("banorte", conversion.GetProperty("suggestedBank").GetString());
        Assert.Equal(Day(_today), conversion.GetProperty("hiredAt").GetString());

        var createdResponse = await _owner.PostAsJsonAsync("/v1/employees", Alta(personId, "banorte", candidateId));
        Assert.True(createdResponse.IsSuccessStatusCode, await createdResponse.Content.ReadAsStringAsync());
        var created = await BodyOf(createdResponse);
        Assert.Equal(Day(_today), created.GetProperty("hiredAt").GetString());
        var employeeId = created.GetProperty("id").GetGuid();
        Assert.Equal(employeeId, await WithDb(db => db.Set<Candidate>().Where(c => c.Uid == candidateId).Select(c => c.EmployeeId).SingleAsync()));
        Assert.Equal(EmployeeEndpoints.CandidateConverted, await DetailOf(await _owner.PostAsJsonAsync("/v1/employees", Alta(personId, "banorte", candidateId))));
    }

    [Fact]
    public async Task A_cambio_de_cliente_o_puesto_is_kept_in_the_audit_trail()
    {
        await Arrange();
        var employee = await Hire((await WithDb(db => db.AddPerson())).Uid);
        var moved = await BodyOf(await _owner.PostAsJsonAsync($"/v1/employees/{employee}/assignment",
            new { payrollGroupId = _group, positionId = _supervisor, shift = "C/NOCHE" }));
        Assert.Equal("SUPERVISOR", moved.GetProperty("positionName").GetString());
        Assert.Equal("585.71", moved.GetProperty("rates").GetProperty("dailySalaryPocket").GetString());

        var audit = await WithDb(db => db.Set<AuditEntry>().Where(a => a.TableName == "employees" && a.Action == AuditAction.Update).SingleAsync());
        Assert.Contains(_validador.ToString(), audit.Before);
        Assert.Contains(_supervisor.ToString(), audit.After);
    }

    [Fact]
    public async Task A_client_scoped_personal_grant_sees_only_its_clients_employees()
    {
        await Arrange();
        var employee = await Hire((await WithDb(db => db.AddPerson())).Uid);
        var heb = (await WithDb(db => db.AddClient("HEB"))).Uid;
        var (_, hebOnly) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollStaff, ClientId = heb });
        var (_, ecoviaOnly) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollStaff, ClientId = _ecovia });

        Assert.Empty((await BodyOf(await hebOnly.GetAsync("/v1/employees"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await hebOnly.GetAsync($"/v1/employees/{employee}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hebOnly.PostAsJsonAsync($"/v1/employees/{employee}/status",
            new { status = "vacation", startDate = Day(_today), endDate = Day(_today), reason = (string?)null, reference = (string?)null })).StatusCode);
        Assert.Single((await BodyOf(await ecoviaOnly.GetAsync("/v1/employees"))).EnumerateArray());

        Assert.Equal(HttpStatusCode.Forbidden, (await (await As(UserRole.Recruiter)).GetAsync("/v1/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await As(UserRole.Administrator, PermissionModule.Recruiting)).GetAsync("/v1/employees")).StatusCode);
    }
}
