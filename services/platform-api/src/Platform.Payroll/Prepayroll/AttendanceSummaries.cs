using Platform.Payroll.Engine;
using Platform.Payroll.Staff;

namespace Platform.Payroll.Prepayroll;

/// <summary>
/// Attendance of a period → the engine's <see cref="AttendanceSummary"/> (PLAN D84), the rule SEM 38 itself implies:
/// <list type="bullet">
/// <item>A día laborado (DIAS LABORADOS) is a <c>present</c>, <c>rest_day_worked</c> or <c>holiday_worked</c> day — and, as
/// paid time off, <c>vacation</c> and <c>leave_paid</c> (open with the owner). A <c>present</c> on a holiday date is an
/// ordinary día; only <c>holiday_worked</c> pays FESTIVO LABORADO.</item>
/// <item><c>partial</c> is not a día: its hours worked are HORAS INCOMPLETAS.</item>
/// <item><c>holiday</c> on a holiday date is a paid día (the descanso obligatorio, LFT art. 74) and entitles FESTIVO PRIMERA
/// MITAD (D51). SEM 38 row 118 shows it: hired on the 15th, F = 3 with O — the 15th, the 16th and the 17th.</item>
/// <item>Horas extra = Σ extra hours of every day; prima dominical when a worked day is a Sunday.</item>
/// <item>Each <c>rest_day_worked</c> also pays DESCANSO LABORADO (the group's amount, 500 by default).</item>
/// </list>
/// </summary>
public static class AttendanceSummaries
{
    private static readonly AttendanceStatus[] CountsAsDay =
    [
        AttendanceStatus.Present, AttendanceStatus.RestDayWorked, AttendanceStatus.HolidayWorked, AttendanceStatus.Vacation,
        AttendanceStatus.LeavePaid,
    ];

    private static readonly AttendanceStatus[] Worked =
        [AttendanceStatus.Present, AttendanceStatus.Partial, AttendanceStatus.RestDayWorked, AttendanceStatus.HolidayWorked];

    /// <summary>
    /// The days that fall in the employment: from <c>HiredAt</c> up to, not including, <c>TerminatedAt</c> (the baja date is the
    /// first day not worked, as in «Cerrar captura»). Rows outside it stay in the database but are never paid.
    /// </summary>
    public static List<AttendanceRecord> WithinEmployment(IEnumerable<AttendanceRecord> days, Employee employee) =>
        WithinEmployment(days, employee.HiredAt, employee.TerminatedAt);

    public static List<AttendanceRecord> WithinEmployment(IEnumerable<AttendanceRecord> days, DateOnly hiredAt, DateOnly? terminatedAt) =>
        days.Where(d => d.WorkDate >= hiredAt && (terminatedAt is not { } t || d.WorkDate < t)).ToList();

    public static AttendanceSummary Summarize(IEnumerable<AttendanceRecord> days, IReadOnlySet<DateOnly> holidays)
    {
        var list = days.ToList();
        return new AttendanceSummary(
            DaysWorked: list.Count(d => CountsAsDay.Contains(d.Status) || IsEntitled(d, holidays)),
            WorkedSunday: list.Any(d => Worked.Contains(d.Status) && d.WorkDate.DayOfWeek == DayOfWeek.Sunday),
            PartialHours: list.Where(d => d.Status == AttendanceStatus.Partial).Sum(d => d.HoursWorked ?? 0),
            OvertimeHours: list.Sum(d => d.OvertimeHours),
            HolidaysEntitled: list.Count(d => IsEntitled(d, holidays)),
            HolidaysWorked: list.Count(d => d.Status == AttendanceStatus.HolidayWorked));
    }

    private static bool IsEntitled(AttendanceRecord d, IReadOnlySet<DateOnly> holidays) =>
        d.Status == AttendanceStatus.Holiday && holidays.Contains(d.WorkDate);

    /// <summary>The DESCANSO LABORADO items the attendance pays (one per rest day worked).</summary>
    public static IEnumerable<IncidentInput> RestDaysWorked(IEnumerable<AttendanceRecord> days, decimal amount) =>
        days.Where(d => d.Status == AttendanceStatus.RestDayWorked).Select(_ => new IncidentInput(PayConcept.RestDayWorked, Amount: amount));
}
