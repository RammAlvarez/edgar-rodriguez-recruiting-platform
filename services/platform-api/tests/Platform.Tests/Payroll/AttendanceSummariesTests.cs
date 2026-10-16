using Platform.Payroll.Engine;
using Platform.Payroll.Prepayroll;
using Platform.Payroll.Staff;

namespace Platform.Tests.Payroll;

/// <summary>Attendance → engine summary (PLAN D84), on the patterns SEM 38 shows (11–17 sep 2026, 16 sep a holiday).</summary>
public sealed class AttendanceSummariesTests
{
    private static readonly HashSet<DateOnly> Holidays = [new(2026, 9, 16)];

    private static AttendanceRecord Day(int day, AttendanceStatus status, decimal? hours = null, decimal overtime = 0) =>
        new() { WorkDate = new DateOnly(2026, 9, day), Status = status, HoursWorked = hours, OvertimeHours = overtime };

    [Fact]
    public void The_holiday_off_is_a_paid_dia_and_entitles_festivo_primera_mitad()
    {
        var week = new[] { 11, 12, 14, 15, 17 }.Select(d => Day(d, AttendanceStatus.Present)).Append(Day(16, AttendanceStatus.Holiday))
            .Append(Day(13, AttendanceStatus.RestDay));
        var s = AttendanceSummaries.Summarize(week, Holidays);
        // SEM 38: F = 6 with O and no prima dominical.
        Assert.Equal((6m, false, 1, 0m), (s.DaysWorked, s.WorkedSunday, s.HolidaysEntitled, s.HolidaysWorked));
    }

    [Fact]
    public void A_partial_day_is_hours_not_a_dia_and_overtime_adds_up()
    {
        var week = new[]
        {
            Day(11, AttendanceStatus.Present, 12, 3), Day(12, AttendanceStatus.Present), Day(14, AttendanceStatus.Partial, 10),
            Day(15, AttendanceStatus.Present, 12, 1.5m), Day(13, AttendanceStatus.RestDay),
        };
        var s = AttendanceSummaries.Summarize(week, Holidays);
        Assert.Equal((3m, false, 10m, 4.5m), (s.DaysWorked, s.WorkedSunday, s.PartialHours, s.OvertimeHours));
    }

    [Fact]
    public void A_rest_day_worked_is_a_dia_and_pays_descanso_laborado()
    {
        var week = new[] { Day(11, AttendanceStatus.Present), Day(13, AttendanceStatus.RestDayWorked) };
        Assert.Equal(2m, AttendanceSummaries.Summarize(week, Holidays).DaysWorked);
        Assert.True(AttendanceSummaries.Summarize(week, Holidays).WorkedSunday);
        var item = Assert.Single(AttendanceSummaries.RestDaysWorked(week, 500m));
        Assert.Equal((PayConcept.RestDayWorked, 500m), (item.Concept, item.Amount));
    }

    [Fact]
    public void Present_on_the_holiday_is_an_ordinary_dia_and_holiday_worked_pays_festivo_laborado()
    {
        Assert.Equal((1m, 0, 0m), Tuple(AttendanceSummaries.Summarize([Day(16, AttendanceStatus.Present)], Holidays)));
        Assert.Equal((1m, 0, 1m), Tuple(AttendanceSummaries.Summarize([Day(16, AttendanceStatus.HolidayWorked)], Holidays)));
        // «holiday» on a day that is not in the catalog entitles nothing.
        Assert.Equal((0m, 0, 0m), Tuple(AttendanceSummaries.Summarize([Day(15, AttendanceStatus.Holiday)], Holidays)));
    }

    [Theory]
    [InlineData(AttendanceStatus.Vacation, 1)]
    [InlineData(AttendanceStatus.LeavePaid, 1)]
    [InlineData(AttendanceStatus.SickLeave, 0)]
    [InlineData(AttendanceStatus.LeaveUnpaid, 0)]
    [InlineData(AttendanceStatus.Absent, 0)]
    [InlineData(AttendanceStatus.RestDay, 0)]
    public void Paid_time_off_counts_as_a_dia_and_the_rest_does_not(AttendanceStatus status, int days) =>
        Assert.Equal(days, AttendanceSummaries.Summarize([Day(12, status)], Holidays).DaysWorked);

    private static (decimal, int, decimal) Tuple(AttendanceSummary s) => (s.DaysWorked, s.HolidaysEntitled, s.HolidaysWorked);
}
