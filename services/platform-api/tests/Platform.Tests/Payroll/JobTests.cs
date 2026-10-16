using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.Internal;
using Platform.Payroll.Periods;
using Platform.Payroll.Staff;
using Platform.Tests.Support;

namespace Platform.Tests.Payroll;

/// <summary>The scheduled jobs (ARCHITECTURE §4): zone-aware, idempotent, on a fixed clock.</summary>
public sealed class JobTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _owner = null!;

    private async Task<object> Run(string name)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetServices<IJob>().Single(j => j.Name == name);
        return await job.RunAsync(default);
    }

    private static int Count(object result, string property) =>
        JsonSerializer.SerializeToElement(result).GetProperty(property).GetInt32();

    /// <summary>A group (weekly, cut-off jueves, lock 23:59 unless given) with one employee hired 5 jan 2026.</summary>
    private async Task<(Guid Group, Guid Employee)> Arrange(string name = "Validadores 12 hrs", string? timeZone = null, string lockTime = "23:59:00")
    {
        _owner ??= await AsOwner();
        var client = await WithDb(db => db.AddClient(name.Split(' ')[0].ToUpperInvariant() + Guid.NewGuid().ToString("N")[..4]));
        var body = JsonSerializer.SerializeToNode(PayrollGroupTests.NewGroup(client.Uid, name))!.AsObject();
        body["timeZone"] = timeZone;
        body["attendanceLockTime"] = lockTime;
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", body))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions", PayrollGroupTests.NewPosition(validFrom: "2026-01-01"))))
            .GetProperty("positions").EnumerateArray().Single().GetProperty("id").GetGuid();
        var person = await WithDb(db => db.AddPerson("ROSA", "LEAL", "GARZA"));
        var employee = (await BodyOf(await _owner.PostAsJsonAsync("/v1/employees", new
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
        return (group, employee);
    }

    private Task Day(Guid employee, DateOnly date, AttendanceStatus status = AttendanceStatus.Present) => WithDb(async db =>
    {
        db.Add(new AttendanceRecord { EmployeeId = employee, WorkDate = date, Status = status, HoursWorked = 12, RecordedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    });

    [Fact]
    public async Task Period_roll_creates_the_current_and_next_period_once()
    {
        Factory.Clock.SetMonterrey(new DateTime(2026, 9, 14, 3, 0, 0)); // lunes, SEM 38
        var (group, _) = await Arrange();
        Assert.Equal(2, Count(await Run("period-roll"), "periodsCreated"));
        Assert.Equal(0, Count(await Run("period-roll"), "periodsCreated"));
        var periods = await WithDb(db => db.Set<PayrollPeriod>().Where(p => p.PayrollGroupId == group).OrderBy(p => p.StartDate).ToListAsync());
        Assert.Equal([(38, new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 17)), (39, new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 24))],
            periods.Select(p => (p.PeriodNumber, p.StartDate, p.EndDate)));
    }

    [Fact]
    public async Task Attendance_lock_locks_each_groups_days_at_its_own_hour_and_only_once()
    {
        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 14, 10, 0, 0));
        var (_, monterrey) = await Arrange();
        var (_, tijuana) = await Arrange("Tijuana 8 hrs", "America/Tijuana");
        await Day(monterrey, new DateOnly(2026, 10, 14));
        await Day(tijuana, new DateOnly(2026, 10, 14));

        // 23:59 in Monterrey is 22:59 in Tijuana: only the Monterrey day has closed.
        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 14, 23, 59, 0));
        Assert.Equal(1, Count(await Run("attendance-lock"), "locked"));
        Assert.Equal(0, Count(await Run("attendance-lock"), "locked"));
        var locked = await WithDb(db => db.Set<AttendanceRecord>().ToDictionaryAsync(a => a.EmployeeId, a => a.IsLocked));
        Assert.True(locked[monterrey]);
        Assert.False(locked[tijuana]);

        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 15, 0, 59, 0)); // 23:59 in Tijuana
        Assert.Equal(1, Count(await Run("attendance-lock"), "locked"));
    }

    [Fact]
    public async Task When_the_last_day_locks_the_period_goes_to_review_and_every_uncaptured_day_is_an_automatic_falta()
    {
        Factory.Clock.SetMonterrey(new DateTime(2026, 9, 14, 3, 0, 0));
        var (group, employee) = await Arrange();
        await Run("period-roll");
        foreach (var d in new[] { 11, 12, 13, 14, 15 })
            await Day(employee, new DateOnly(2026, 9, d));

        Factory.Clock.SetMonterrey(new DateTime(2026, 9, 17, 23, 0, 0)); // the last day (jueves) is still open
        Assert.Equal(0, Count(await Run("attendance-lock"), "periodsInReview"));

        Factory.Clock.SetMonterrey(new DateTime(2026, 9, 17, 23, 59, 0));
        var result = await Run("attendance-lock");
        Assert.Equal((1, 2), (Count(result, "periodsInReview"), Count(result, "autoAbsences"))); // 16 and 17 sep
        var second = await Run("attendance-lock");
        Assert.Equal((0, 0), (Count(second, "periodsInReview"), Count(second, "autoAbsences")));

        var period = await WithDb(db => db.Set<PayrollPeriod>().SingleAsync(p => p.PayrollGroupId == group && p.PeriodNumber == 38));
        Assert.Equal(PeriodStatus.InReview, period.Status);
        var auto = await WithDb(db => db.Set<AttendanceRecord>().Where(a => a.IsAutoAbsent).OrderBy(a => a.WorkDate).ToListAsync());
        Assert.Equal([new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 17)], auto.Select(a => a.WorkDate));
        Assert.All(auto, a => Assert.True(a.IsLocked && a.Status == AttendanceStatus.Absent));
    }

    [Fact]
    public async Task Status_roll_returns_an_expired_vacation_to_activo()
    {
        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 14, 10, 0, 0));
        var (_, employee) = await Arrange();
        await _owner.PostAsJsonAsync($"/v1/employees/{employee}/status",
            new { status = "vacation", startDate = "2026-10-12", endDate = "2026-10-14", reason = (string?)null, reference = (string?)null });
        Assert.Equal(EmployeeStatus.Vacation, await WithDb(db => db.Set<Employee>().Select(e => e.Status).SingleAsync()));

        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 15, 2, 5, 0));
        Assert.Equal(1, Count(await Run("status-roll"), "statusesChanged"));
        Assert.Equal(0, Count(await Run("status-roll"), "statusesChanged"));
        Assert.Equal(EmployeeStatus.Active, await WithDb(db => db.Set<Employee>().Select(e => e.Status).SingleAsync()));
    }

    [Fact]
    public async Task The_scheduler_runs_them_by_name_with_the_internal_secret()
    {
        Factory.Clock.SetMonterrey(new DateTime(2026, 9, 14, 3, 0, 0));
        await Arrange();
        var http = Client();
        http.DefaultRequestHeaders.Add(InternalEndpoints.SecretHeader, ApiFactory.InternalSecret);
        foreach (var name in new[] { "period-roll", "attendance-lock", "status-roll" })
            Assert.Equal(HttpStatusCode.OK, (await http.PostAsync($"/internal/jobs/{name}", null)).StatusCode);
        Assert.Equal(2, await WithDb(db => db.Set<PayrollPeriod>().CountAsync()));
    }
}
