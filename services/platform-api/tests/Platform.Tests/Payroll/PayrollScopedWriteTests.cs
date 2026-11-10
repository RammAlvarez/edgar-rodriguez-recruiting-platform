using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Payroll.Staff;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>
/// Every mutating payroll route follows the client scope of the caller's grant: a user whose grants are limited to another client
/// gets 404 (never a write, never a 403 that would confirm the record exists). Each case also runs the same request as a user
/// scoped to the right client, so a 404 cannot come from a malformed request.
/// </summary>
public sealed class PayrollScopedWriteTests(ApiFactory factory) : ApiTest(factory)
{
    private static readonly PermissionModule[] Modules =
        [PermissionModule.PayrollPrepayroll, PermissionModule.PayrollAttendance, PermissionModule.PayrollIncidents, PermissionModule.PayrollStaff];

    private HttpClient _owner = null!, _inScope = null!, _otherClient = null!;
    private Guid _ecovia, _group, _position, _employee, _period, _line, _site, _incident, _schedule, _statusPeriod, _account, _bank;

    private async Task Arrange()
    {
        _owner = await AsOwner();
        var (ecovia, heb) = await WithDb(async db => (await db.AddClient("ECOVIA"), await db.AddClient("HEB")));
        _ecovia = ecovia.Uid;
        _site = await WithDb(async db =>
        {
            var site = new ClientSite { ClientId = ecovia.Uid, Name = "ECOVIA — Estación Centro" };
            db.Add(site);
            await db.SaveChangesAsync();
            return site.Uid;
        });
        _group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(ecovia.Uid)))).GetProperty("id").GetGuid();
        _position = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{_group}/positions", PayrollGroupTests.NewPosition(validFrom: "2026-01-01"))))
            .GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        var person = await WithDb(db => db.AddPerson("ROSA", "GARZA", "SOTO"));
        _employee = (await BodyOf(await _owner.PostAsJsonAsync("/v1/employees", new
        {
            personId = person.Uid,
            payrollGroupId = _group,
            positionId = _position,
            shift = "C/DÍA",
            hiredAt = "2026-01-05",
            dailySalaryPocketOverride = (string?)null,
            dailySalaryImssOverride = (string?)null,
            hourlyRateOverride = (string?)null,
            daysWorkedBonusOverride = (string?)null,
            notes = (string?)null,
            cardBank = "banorte",
        }))).GetProperty("id").GetGuid();

        _period = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{_group}/periods", new { date = "2026-09-14" }))).GetProperty("id").GetGuid();
        _incident = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/incidents", IncidentBody()))).GetProperty("id").GetGuid();
        _line = (await BodyOf(await _owner.GetAsync($"/v1/payroll/periods/{_period}"))).GetProperty("rows").EnumerateArray().Single().GetProperty("lineId").GetGuid();
        _schedule = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", ScheduleBody())))
            .EnumerateArray().Single().GetProperty("id").GetGuid();
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/status", new { status = "vacation", startDate = "2026-11-02", endDate = "2026-11-04", reason = (string?)null, reference = (string?)null });
        _statusPeriod = await WithDb(db => db.Set<EmployeeStatusPeriod>().Where(p => p.EmployeeId == _employee).Select(p => p.Uid).SingleAsync());
        _bank = await WithDb(db => db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.SpeiCode == "012").Select(b => b.Uid).SingleAsync());
        _account = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/bank-accounts", AccountBody()))).EnumerateArray().Single().GetProperty("id").GetGuid();

        var grants = (Guid client) => Modules.Select(m => new UserPermission { Module = m, ClientId = client }).ToArray();
        _inScope = (await SignedIn(UserRole.Administrator, grants(ecovia.Uid))).Http;
        _otherClient = (await SignedIn(UserRole.Administrator, grants(heb.Uid))).Http;
    }

    private object IncidentBody() => new
    {
        employeeId = _employee,
        conceptCode = "OVERTIME_DOUBLE",
        date = "2026-09-12",
        quantity = "2",
        amount = (string?)null,
        reason = "Evento",
    };

    private object ScheduleBody() => new
    {
        clientSiteId = _site,
        weekdays = new[] { 1, 2, 3 },
        oneOffDate = (string?)null,
        startTime = "07:00:00",
        endTime = "19:00:00",
        validFrom = "2026-01-01",
        validTo = (string?)null,
        jobDescription = (string?)null,
    };

    private object AccountBody() => new
    {
        bankId = _bank,
        accountType = "clabe",
        accountNumber = "012580015000000011",
        holderName = "Rosa Garza Soto",
        alias = (string?)null,
        validFrom = (string?)null,
        makePrimary = false,
    };

    private static object Rates(string pocket = "430") => new
    {
        dailySalaryPocket = pocket,
        dailySalaryImss = "278.80",
        hourlyRate = "35.7142",
        daysWorkedBonus = "450",
        validFrom = "2026-12-01",
    };

    /// <summary>Same request as the caller scoped to another client (404, nothing written) and as the one scoped to the right client (any answer but 404/403).</summary>
    private async Task Route(string name, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        var denied = await send(_otherClient);
        Assert.True(denied.StatusCode == HttpStatusCode.NotFound, $"{name}: another client's grant got {(int)denied.StatusCode}, expected 404");
        var allowed = await send(_inScope);
        Assert.True(allowed.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Forbidden),
            $"{name}: the right client's grant got {(int)allowed.StatusCode}, so the 404 above proves nothing");
    }

    [Fact]
    public async Task Prenomina_writes_follow_the_client_scope()
    {
        await Arrange();
        await Route("create period", h => h.PostAsJsonAsync($"/v1/payroll/groups/{_group}/periods", new { date = "2026-09-21" }));
        await Route("line comments", h => h.PutAsJsonAsync($"/v1/payroll/lines/{_line}/comments", new { comments = "Nota" }));
        await Route("line adjustment", h => h.PostAsJsonAsync($"/v1/payroll/lines/{_line}/adjustments", new { column = "SUELDO", amount = "2600", reason = "Turno doble" }));
        await Route("export", h => h.PostAsJsonAsync($"/v1/payroll/periods/{_period}/exports", new { kind = "summary" }));
        await Route("close capture", h => h.PostAsync($"/v1/payroll/periods/{_period}/close-capture", null));
    }

    [Fact]
    public async Task Incident_writes_follow_the_client_scope()
    {
        await Arrange();
        await Route("create incident", h => h.PostAsJsonAsync("/v1/payroll/incidents", IncidentBody()));
        await Route("update incident", h => h.PutAsJsonAsync($"/v1/payroll/incidents/{_incident}", IncidentBody()));
        await Route("delete incident", h => h.DeleteAsync($"/v1/payroll/incidents/{_incident}"));
    }

    [Fact]
    public async Task Attendance_writes_follow_the_client_scope()
    {
        await Arrange();
        await Route("save day", h => h.PutAsJsonAsync("/v1/attendance/day", new
        {
            siteId = _site,
            date = "2026-09-14",
            entries = new[] { new { employeeId = _employee, status = "present", hoursWorked = (string?)null, overtimeHours = (string?)null, note = (string?)null } },
        }));
    }

    [Fact]
    public async Task Employee_writes_follow_the_client_scope()
    {
        await Arrange();
        await Route("update employee", h => h.PutAsJsonAsync($"/v1/employees/{_employee}", new
        {
            shift = "C/DÍA",
            dailySalaryPocketOverride = (string?)null,
            dailySalaryImssOverride = (string?)null,
            hourlyRateOverride = (string?)null,
            daysWorkedBonusOverride = (string?)null,
            notes = "Nota",
        }));
        await Route("assignment", h => h.PostAsJsonAsync($"/v1/employees/{_employee}/assignment", new { payrollGroupId = _group, positionId = _position, shift = "C/NOCHE" }));
        await Route("add status", h => h.PostAsJsonAsync($"/v1/employees/{_employee}/status",
            new { status = "vacation", startDate = "2026-12-07", endDate = "2026-12-08", reason = (string?)null, reference = (string?)null }));
        await Route("update status", h => h.PutAsJsonAsync($"/v1/employees/{_employee}/status/{_statusPeriod}", new { endDate = "2026-11-05", reference = (string?)null }));
        await Route("end status", h => h.PostAsync($"/v1/employees/{_employee}/status/{_statusPeriod}/end", null));
        await Route("rehire", h => h.PostAsJsonAsync($"/v1/employees/{_employee}/rehire",
            new { payrollGroupId = _group, positionId = _position, shift = "C/DÍA", hiredAt = "2027-01-04", cardBank = (string?)null }));
        await Route("card request", h => h.PostAsJsonAsync($"/v1/employees/{_employee}/card-request", new { bank = "banregio" }));
        await Route("add bank account", h => h.PostAsJsonAsync($"/v1/employees/{_employee}/bank-accounts", AccountBody()));
        await Route("update bank account", h => h.PutAsJsonAsync($"/v1/employees/{_employee}/bank-accounts/{_account}",
            new { holderName = "Rosa Garza Soto", alias = "Nómina", validTo = (string?)null }));
        await Route("make account primary", h => h.PostAsync($"/v1/employees/{_employee}/bank-accounts/{_account}/primary", null));
    }

    [Fact]
    public async Task Schedule_writes_follow_the_client_scope()
    {
        await Arrange();
        await Route("create schedule", h => h.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", ScheduleBody()));
        await Route("update schedule", h => h.PutAsJsonAsync($"/v1/employees/{_employee}/schedules/{_schedule}", ScheduleBody()));
        await Route("delete schedule", h => h.DeleteAsync($"/v1/employees/{_employee}/schedules/{_schedule}"));
    }

    [Fact]
    public async Task Group_and_puesto_writes_follow_the_client_scope()
    {
        await Arrange();
        await Route("update group", h => h.PutAsJsonAsync($"/v1/payroll/groups/{_group}", PayrollGroupTests.NewGroup(_ecovia)));
        await Route("add position", h => h.PostAsJsonAsync($"/v1/payroll/groups/{_group}/positions", PayrollGroupTests.NewPosition("Supervisor", "585.71", "2026-01-01")));
        await Route("group concepts", h => h.PutAsJsonAsync($"/v1/payroll/groups/{_group}/concepts", new { concepts = Array.Empty<object>() }));
        await Route("rename position", h => h.PutAsJsonAsync($"/v1/payroll/positions/{_position}", new { name = "Validador", isActive = true }));
        await Route("position rates", h => h.PostAsJsonAsync($"/v1/payroll/positions/{_position}/rates", Rates()));
    }
}
