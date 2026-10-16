using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Payroll.Staff;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>Asistencia (SPEC §5.3, DATA_MODEL §5.7–5.8, PLAN D83), on a fixed clock: 14 oct 2026 in Monterrey.</summary>
public sealed class AttendanceTests(ApiFactory factory) : ApiTest(factory)
{
    private static readonly DateOnly Today = new(2026, 10, 14);
    private HttpClient _owner = null!, _supervisor = null!;
    private Guid _ecovia, _centro, _hebSite, _employee, _rest;

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task Arrange()
    {
        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 14, 10, 0, 0));
        (_ecovia, _centro, _hebSite) = await WithDb(async db =>
        {
            var ecovia = await db.AddClient("ECOVIA");
            var heb = await db.AddClient("HEB");
            var centro = new ClientSite { ClientId = ecovia.Uid, Name = "ECOVIA — Estación Centro" };
            var cumbres = new ClientSite { ClientId = heb.Uid, Name = "HEB — Oficinas Cumbres" };
            db.AddRange(centro, cumbres);
            await db.SaveChangesAsync();
            return (ecovia.Uid, centro.Uid, cumbres.Uid);
        });
        _owner = await AsOwner();
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(_ecovia)))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions", PayrollGroupTests.NewPosition(validFrom: "2026-01-01"))))
            .GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        _employee = await Hire(group, position, "ROSA");
        _rest = await Hire(group, position, "BETO");
        foreach (var e in new[] { _employee, _rest })
            await _owner.PostAsJsonAsync($"/v1/employees/{e}/schedules", new
            {
                clientSiteId = _centro,
                weekdays = new[] { 1, 2, 3, 4, 5, 6, 7 },
                oneOffDate = (string?)null,
                startTime = "07:00:00",
                endTime = "19:00:00",
                validFrom = "2026-01-01",
                validTo = (string?)null,
                jobDescription = (string?)null,
            });
        var (_, supervisor) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollAttendance, ClientId = _ecovia });
        _supervisor = supervisor;
    }

    private async Task<Guid> Hire(Guid group, Guid position, string name)
    {
        var person = await WithDb(db => db.AddPerson(name, "GARZA", "SOTO"));
        return (await BodyOf(await _owner.PostAsJsonAsync("/v1/employees", new
        {
            personId = person.Uid,
            payrollGroupId = group,
            positionId = position,
            shift = "C/DÍA",
            hiredAt = "2026-01-05",
            dailySalaryPocketOverride = (string?)null,
            dailySalaryImssOverride = (string?)null,
            hourlyRateOverride = (string?)null,
            daysWorkedBonusOverride = (string?)null,
            notes = (string?)null,
            cardBank = "banorte",
            candidateId = (Guid?)null,
        }))).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> Save(HttpClient http, DateOnly date, params object[] entries) =>
        http.PutAsJsonAsync("/v1/attendance/day", new { siteId = _centro, date = Day(date), entries });

    private static object Entry(Guid employee, string status, string? hours = null, string? overtime = null) =>
        new { employeeId = employee, status, hoursWorked = hours, overtimeHours = overtime, note = (string?)null };

    private object Unlock(Guid employee, DateOnly date, string status, string? reason = "Lo reportó tarde el supervisor") => new
    {
        employeeId = employee,
        date = Day(date),
        siteId = _centro,
        status,
        hoursWorked = (string?)null,
        overtimeHours = (string?)null,
        note = (string?)null,
        reason,
    };

    [Fact]
    public async Task A_supervisor_captures_the_roster_of_a_site_of_their_client()
    {
        await Arrange();
        var sites = (await BodyOf(await _supervisor.GetAsync($"/v1/attendance/sites?date={Day(Today)}"))).EnumerateArray().ToList();
        Assert.Equal(("ECOVIA — Estación Centro", 2, 0), (sites.Single().GetProperty("siteName").GetString(),
            sites.Single().GetProperty("expected").GetInt32(), sites.Single().GetProperty("captured").GetInt32()));

        var saved = await BodyOf(await Save(_supervisor, Today, Entry(_employee, "present"), Entry(_rest, "partial", "8", "1.5")));
        var entries = saved.GetProperty("entries").EnumerateArray().ToDictionary(e => e.GetProperty("employeeId").GetGuid());
        Assert.Equal("12.00", entries[_employee].GetProperty("record").GetProperty("hoursWorked").GetString()); // the shift
        Assert.Equal(("8.00", "1.50"), (entries[_rest].GetProperty("record").GetProperty("hoursWorked").GetString(),
            entries[_rest].GetProperty("record").GetProperty("overtimeHours").GetString()));
        Assert.False(saved.GetProperty("canUnlock").GetBoolean());

        Assert.Contains("entries.1.hoursWorked", (await ErrorsOf(await Save(_supervisor, Today, Entry(_employee, "present"), Entry(_rest, "partial", "12")))).Keys);
        Assert.Contains("entries.0.overtimeHours", (await ErrorsOf(await Save(_supervisor, Today, Entry(_employee, "absent", null, "2")))).Keys);
        Assert.Equal(HttpStatusCode.NotFound, (await _supervisor.GetAsync($"/v1/attendance/day?siteId={_hebSite}&date={Day(Today)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Save(_supervisor, Today.AddDays(1), Entry(_employee, "present"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await As(UserRole.Administrator, PermissionModule.PayrollStaff)).GetAsync($"/v1/attendance/sites?date={Day(Today)}")).StatusCode);
    }

    [Fact]
    public async Task A_day_locks_at_the_groups_lock_time_whether_or_not_it_was_captured()
    {
        await Arrange();
        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 14, 23, 58, 0));
        Assert.Equal(HttpStatusCode.OK, (await Save(_supervisor, Today, Entry(_employee, "present"))).StatusCode);

        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 14, 23, 59, 0));
        var late = await Save(_supervisor, Today, Entry(_employee, "absent"));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.EndsWith(AttendanceRules.Locked, await DetailOf(late));
        Assert.Equal(HttpStatusCode.Conflict, (await Save(_supervisor, Today, Entry(_rest, "present"))).StatusCode); // never captured

        var day = await BodyOf(await _owner.GetAsync($"/v1/attendance/day?siteId={_centro}&date={Day(Today)}"));
        Assert.All(day.GetProperty("entries").EnumerateArray(), e => Assert.True(e.GetProperty("locked").GetBoolean()));
    }

    [Fact]
    public async Task Only_an_all_clients_grant_unlocks_and_every_change_keeps_its_reason_and_values()
    {
        await Arrange();
        var yesterday = Today.AddDays(-1);
        Assert.Equal(AttendanceRules.UnlockNeedsAllClients, await DetailOf(await _supervisor.PostAsJsonAsync("/v1/attendance/unlock", Unlock(_employee, yesterday, "present"))));
        Assert.Equal(AttendanceRules.NotLocked, await DetailOf(await _owner.PostAsJsonAsync("/v1/attendance/unlock", Unlock(_employee, Today, "present"))));
        Assert.Contains("reason", (await ErrorsOf(await _owner.PostAsJsonAsync("/v1/attendance/unlock", Unlock(_employee, yesterday, "present", " ")))).Keys);

        var first = await BodyOf(await _owner.PostAsJsonAsync("/v1/attendance/unlock", Unlock(_employee, yesterday, "absent")));
        Assert.True(first.GetProperty("isLocked").GetBoolean());
        await _owner.PostAsJsonAsync("/v1/attendance/unlock", Unlock(_employee, yesterday, "present", "Sí asistió, se corrigió"));

        var log = await WithDb(db => db.Set<AttendanceUnlock>().OrderBy(u => u.Id).ToListAsync());
        Assert.Equal(2, log.Count);
        Assert.Null(log[0].PreviousValue); // the day had no record
        Assert.Contains("Absent", log[0].NewValue);
        Assert.Contains("Absent", log[1].PreviousValue);
        Assert.Contains("Present", log[1].NewValue);
        Assert.Equal("Sí asistió, se corrigió", log[1].Reason);
        Assert.Equal(AttendanceStatus.Present, await WithDb(db => db.Set<AttendanceRecord>().Select(a => a.Status).SingleAsync()));
    }

    [Fact]
    public async Task A_status_period_day_is_changed_only_through_cambiar_estatus()
    {
        await Arrange();
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/status",
            new { status = "vacation", startDate = Day(Today.AddDays(-2)), endDate = Day(Today), reason = (string?)null, reference = (string?)null });
        var capture = await Save(_supervisor, Today, Entry(_employee, "present"));
        Assert.Contains("Cambiar estatus", await DetailOf(capture));
        Assert.Contains("Cambiar estatus", await DetailOf(await _owner.PostAsJsonAsync("/v1/attendance/unlock", Unlock(_employee, Today.AddDays(-1), "present"))));

        var day = await BodyOf(await _supervisor.GetAsync($"/v1/attendance/day?siteId={_centro}&date={Day(Today)}"));
        var rosa = day.GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("employeeId").GetGuid() == _employee);
        Assert.Equal("Vacaciones", rosa.GetProperty("statusPeriod").GetString());
        Assert.True(rosa.GetProperty("record").GetProperty("fromStatusPeriod").GetBoolean());
    }

    [Fact]
    public async Task A_holiday_is_named_on_its_day_and_the_employee_tab_lists_the_days()
    {
        await Arrange();
        await WithDb(async db =>
        {
            db.Add(new Holiday { Date = Today, NameEs = "Día de prueba", IsMandatory = true });
            await db.SaveChangesAsync();
        });
        var day = await BodyOf(await _supervisor.GetAsync($"/v1/attendance/day?siteId={_centro}&date={Day(Today)}"));
        Assert.Equal("Día de prueba", day.GetProperty("holiday").GetString());

        await Save(_supervisor, Today, Entry(_employee, "holiday_worked"));
        var days = (await BodyOf(await _owner.GetAsync($"/v1/employees/{_employee}/attendance?from={Day(Today.AddDays(-6))}&to={Day(Today)}"))).EnumerateArray().ToList();
        Assert.Equal(("holiday_worked", "ECOVIA — Estación Centro"), (days.Single().GetProperty("status").GetString(), days.Single().GetProperty("siteName").GetString()));
    }
}
