using System.Text.Json;
using Platform.Payroll.Groups;

namespace Platform.Payroll.Staff;

/// <summary>
/// Asistencia (SPEC §5.3, PLAN D83). A day is locked at the moment <c>work_date + attendance_lock_time</c> passes in the group's
/// time zone — the capture routes apply that rule themselves, the <c>attendance-lock</c> job only writes <c>is_locked</c> on the
/// records that exist. A locked day, with or without a record, changes only through an unlock with a reason.
/// </summary>
public static class AttendanceRules
{
    public const string Locked = "El día ya cerró. Para cambiarlo pide el desbloqueo con un motivo.";
    public const string NotLocked = "El día sigue abierto: captúralo normalmente.";
    public const string Future = "No se captura asistencia de días que no han llegado.";
    public const string UnlockNeedsAllClients = "Solo un administrador con Asistencia para todos los clientes desbloquea días cerrados.";
    public const string ReasonRequired = "Indica el motivo del cambio.";

    public static string NotEmployed(DateOnly date) => $"No trabajaba en el grupo el {Date(date)}.";

    public static string StatusPeriodDay(EmployeeStatusPeriod p) =>
        $"Es un día de {StaffRules.Label(p.Status)}: se cambia con «Cambiar estatus» en Personal.";

    public static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public static TimeZoneInfo Zone(PayrollGroup g) => TimeZoneInfo.FindSystemTimeZoneById(g.TimeZone);

    /// <summary>The instant the day locks: its lock time on that date in the group's zone.</summary>
    public static DateTimeOffset LockMoment(PayrollGroup g, DateOnly date)
    {
        var local = date.ToDateTime(g.AttendanceLockTime);
        return new DateTimeOffset(local, Zone(g).GetUtcOffset(local)).ToUniversalTime();
    }

    public static bool IsLocked(PayrollGroup g, DateOnly date, DateTimeOffset now) => now >= LockMoment(g, date);

    public static DateOnly Today(PayrollGroup g, DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone(g)).DateTime);

    /// <summary>Statuses that carry hours worked; the rest are whole days with no hours.</summary>
    public static bool Worked(AttendanceStatus s) =>
        s is AttendanceStatus.Present or AttendanceStatus.Partial or AttendanceStatus.RestDayWorked or AttendanceStatus.HolidayWorked;

    /// <summary>
    /// Checks a captured value against the group's shift and returns the hours to store: present defaults to the shift,
    /// partial needs fewer hours than the shift, a day off carries none; overtime only on worked days.
    /// </summary>
    public static (decimal? Hours, decimal Overtime, string? Field, string? Problem) Normalize(PayrollGroup g, AttendanceStatus status,
        decimal? hours, decimal? overtime)
    {
        var extra = overtime ?? 0;
        if (extra < 0 || extra > 24 || decimal.Round(extra, 2) != extra)
            return (null, 0, "overtimeHours", "Horas extra entre 0 y 24.");
        if (!Worked(status) && extra > 0)
            return (null, 0, "overtimeHours", "Solo un día trabajado tiene horas extra.");
        switch (status)
        {
            case AttendanceStatus.Partial:
                if (hours is not { } h || h <= 0 || h >= g.ShiftHours || decimal.Round(h, 2) != h)
                    return (null, 0, "hoursWorked", $"Horas incompletas: más de 0 y menos de {g.ShiftHours:0.##}.");
                return (h, extra, null, null);
            case AttendanceStatus.Present or AttendanceStatus.RestDayWorked or AttendanceStatus.HolidayWorked:
                if (hours is { } p && (p <= 0 || p > 24 || decimal.Round(p, 2) != p))
                    return (null, 0, "hoursWorked", "Horas entre 0 y 24.");
                return (hours ?? g.ShiftHours, extra, null, null);
            default:
                return (null, 0, null, null);
        }
    }

    /// <summary>A day as the unlock log keeps it.</summary>
    public static string Snapshot(AttendanceRecord r) => JsonSerializer.Serialize(new
    {
        status = r.Status.ToString(),
        hoursWorked = r.HoursWorked,
        overtimeHours = r.OvertimeHours,
        clientSiteId = r.ClientSiteId,
        note = r.Note,
    });
}
