using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Core.Abstractions;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Payroll.Staff;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>Work schedules and the Calendario de servicios (SPEC §5.2, DATA_MODEL §5.6).</summary>
public sealed class ScheduleTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _owner = null!;
    private Guid _heb, _cumbres, _employee;
    private DateOnly _monday;

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task Arrange()
    {
        var today = new SystemClock().Today();
        _monday = today.AddDays(-(ScheduleEndpoints.IsoWeekday(today) - 1)).AddDays(7); // next week's Monday
        var heb = await WithDb(async db =>
        {
            var c = await db.AddClient("HEB");
            db.Add(new ClientSite { ClientId = c.Uid, Name = "HEB — Oficinas Cumbres" });
            await db.SaveChangesAsync();
            return c;
        });
        _heb = heb.Uid;
        _cumbres = await WithDb(db => Task.FromResult(db.Set<ClientSite>().Single(s => s.ClientId == heb.Uid).Uid));
        _owner = await AsOwner();
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(_heb, "Limpieza oficinas")))).GetProperty("id").GetGuid();
        var detail = await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions",
            PayrollGroupTests.NewPosition("Intendente", "320.00", Day(today.AddYears(-1)))));
        var position = detail.GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        var person = await WithDb(db => db.AddPerson("LUCÍA", "MENDOZA", "RÍOS"));
        _employee = (await BodyOf(await _owner.PostAsJsonAsync("/v1/employees", new
        {
            personId = person.Uid,
            payrollGroupId = group,
            positionId = position,
            shift = "C/DÍA",
            hiredAt = Day(today.AddDays(-30)),
            dailySalaryPocketOverride = (string?)null,
            dailySalaryImssOverride = (string?)null,
            hourlyRateOverride = (string?)null,
            daysWorkedBonusOverride = (string?)null,
            notes = (string?)null,
            cardBank = "banorte",
            candidateId = (Guid?)null,
        }))).GetProperty("id").GetGuid();
    }

    private object Schedule(short[] weekdays, DateOnly? oneOff = null, string? job = null, Guid? site = null) => new
    {
        clientSiteId = site ?? _cumbres,
        weekdays,
        oneOffDate = oneOff is { } d ? Day(d) : null,
        startTime = "07:00:00",
        endTime = "15:00:00",
        validFrom = (string?)null,
        validTo = (string?)null,
        jobDescription = job,
    };

    private async Task<List<JsonElement>> Calendar(DateOnly from, DateOnly to) =>
        (await BodyOf(await _owner.GetAsync($"/v1/calendar?from={Day(from)}&to={Day(to)}"))).EnumerateArray().ToList();

    [Fact]
    public async Task The_calendar_expands_weekly_and_one_off_schedules_with_the_days_attendance()
    {
        await Arrange();
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", Schedule([1, 2, 3, 4, 5]));
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", Schedule([], _monday.AddDays(5), "Mantenimiento A/C"));
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/status",
            new { status = "vacation", startDate = Day(_monday.AddDays(1)), endDate = Day(_monday.AddDays(1)), reason = (string?)null, reference = (string?)null });

        var week = await Calendar(_monday, _monday.AddDays(6));
        int Expected(int i) => week[i].GetProperty("sites").EnumerateArray().Sum(s => s.GetProperty("entries").GetArrayLength());
        Assert.Equal([1, 1, 1, 1, 1, 1, 0], Enumerable.Range(0, 7).Select(Expected)); // L–V, the Saturday job, no Sunday

        var tuesday = week[1].GetProperty("sites").EnumerateArray().Single();
        Assert.Equal("HEB — Oficinas Cumbres", tuesday.GetProperty("siteName").GetString());
        var lucia = tuesday.GetProperty("entries").EnumerateArray().Single();
        Assert.Equal("MENDOZA RÍOS LUCÍA", lucia.GetProperty("fullName").GetString());
        Assert.Equal("vacation", lucia.GetProperty("attendance").GetString());
        var saturday = week[5].GetProperty("sites").EnumerateArray().Single().GetProperty("entries").EnumerateArray().Single();
        Assert.Equal("Mantenimiento A/C", saturday.GetProperty("jobDescription").GetString());
    }

    [Fact]
    public async Task After_a_baja_the_employee_is_no_longer_expected()
    {
        await Arrange();
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", Schedule([1, 2, 3, 4, 5]));
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/status",
            new { status = "terminated", startDate = Day(_monday.AddDays(2)), endDate = (string?)null, reason = "Renuncia voluntaria", reference = (string?)null });

        var week = await Calendar(_monday, _monday.AddDays(4));
        Assert.Equal([1, 1, 0, 0, 0], week.Select(d => d.GetProperty("sites").EnumerateArray().Sum(s => s.GetProperty("entries").GetArrayLength())));
    }

    [Fact]
    public async Task A_schedule_is_at_a_site_of_the_employees_client_and_either_weekly_or_one_off()
    {
        await Arrange();
        var other = await WithDb(async db =>
        {
            var c = await db.AddClient("ECOVIA");
            var site = new ClientSite { ClientId = c.Uid, Name = "ECOVIA — Estación Centro" };
            db.Add(site);
            await db.SaveChangesAsync();
            return site.Uid;
        });
        Assert.Equal(ScheduleEndpoints.SiteNotOfClient,
            (await ErrorsOf(await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", Schedule([1], site: other))))["clientSiteId"][0]);
        Assert.Contains("weekdays", (await ErrorsOf(await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", Schedule([1], _monday)))).Keys);
        Assert.Contains("weekdays", (await ErrorsOf(await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", Schedule([])))).Keys);
        Assert.Contains("to", (await ErrorsOf(await _owner.GetAsync($"/v1/calendar?from={Day(_monday)}&to={Day(_monday.AddDays(60))}"))).Keys);
    }

    [Fact]
    public async Task The_calendar_needs_asistencia_and_follows_the_client_scope()
    {
        await Arrange();
        await _owner.PostAsJsonAsync($"/v1/employees/{_employee}/schedules", Schedule([1, 2, 3, 4, 5]));
        var ecovia = (await WithDb(db => db.AddClient("ECOVIA"))).Uid;
        var (_, supervisor) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollAttendance, ClientId = ecovia });
        var monday = (await BodyOf(await supervisor.GetAsync($"/v1/calendar?from={Day(_monday)}&to={Day(_monday)}"))).EnumerateArray().Single();
        Assert.Empty(monday.GetProperty("sites").EnumerateArray());

        var staff = await As(UserRole.Administrator, PermissionModule.PayrollStaff);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/v1/calendar?from={Day(_monday)}&to={Day(_monday)}")).StatusCode);
    }
}
