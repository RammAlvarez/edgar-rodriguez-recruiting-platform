using Microsoft.EntityFrameworkCore;
using Platform.Core.People;
using Platform.Payroll.Groups;

namespace Platform.Payroll.Staff;

/// <summary>
/// The rules of Personal (SPEC §5.1, DATA_MODEL §5.4–5.4b, PLAN D82): today's status from the periods, the days a period
/// writes into attendance, the rates an employee is paid at, and the motivos of a baja.
/// </summary>
public static class StaffRules
{
    /// <summary>Motivos de baja (SPEC §5.1 «motivo from catalog»; Rotación groups by them in S11).</summary>
    public static readonly IReadOnlyList<string> TerminationReasons =
    [
        "Renuncia voluntaria",
        "Abandono de trabajo",
        "Término de contrato",
        "Rescisión (despido)",
        "Fin del servicio con el cliente",
        "Otro",
    ];

    public static readonly IReadOnlyList<string> Shifts = ["C/DÍA", "C/NOCHE"];

    public static string Overlaps(EmployeeStatusPeriod other) => $"Se cruza con {Describe(other)}; cámbialo o termínalo primero.";

    public static string BeforeHire(DateOnly hiredAt) => $"La fecha es anterior al ingreso ({Date(hiredAt)}).";
    public const string AlreadyTerminated = "El empleado está dado de baja; usa «Reingreso».";
    public const string BajaIsReingreso = "Una baja no se reactiva; usa «Reingreso», que crea un nuevo ingreso.";

    /// <summary>The period covering <paramref name="date"/> (a baja covers every day from its date on).</summary>
    public static EmployeeStatusPeriod? Covering(IEnumerable<EmployeeStatusPeriod> periods, DateOnly date) =>
        periods.FirstOrDefault(p => p.StartDate <= date && (p.EndDate is null || p.EndDate >= date));

    public static EmployeeStatus StatusOn(IEnumerable<EmployeeStatusPeriod> periods, DateOnly date) =>
        Covering(periods, date)?.Status ?? EmployeeStatus.Active;

    /// <summary>The next period that has not started yet (a scheduled vacation shows on the card).</summary>
    public static EmployeeStatusPeriod? Next(IEnumerable<EmployeeStatusPeriod> periods, DateOnly date) =>
        periods.Where(p => p.StartDate > date).OrderBy(p => p.StartDate).FirstOrDefault();

    /// <summary>The attendance a period means: vacation, sick leave, unpaid leave for a suspension; a baja writes none.</summary>
    public static AttendanceStatus? AttendanceOf(EmployeeStatus status) => status switch
    {
        EmployeeStatus.Vacation => AttendanceStatus.Vacation,
        EmployeeStatus.SickLeave => AttendanceStatus.SickLeave,
        EmployeeStatus.Suspended => AttendanceStatus.LeaveUnpaid,
        _ => null,
    };

    public static string Label(EmployeeStatus status) => status switch
    {
        EmployeeStatus.Vacation => "Vacaciones",
        EmployeeStatus.SickLeave => "Incapacidad",
        EmployeeStatus.Suspended => "Suspensión",
        EmployeeStatus.Terminated => "Baja",
        _ => "Activo",
    };

    public static string Describe(EmployeeStatusPeriod p) =>
        $"{Label(p.Status)} {Date(p.StartDate)}" + (p.EndDate is { } e ? $" – {Date(e)}" : "");

    public static string Date(DateOnly d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes the period's days into attendance, locked, so the prenómina grid shows them without the supervisor (DATA_MODEL
    /// §5.4b). A day already captured is replaced: the period wins.
    /// </summary>
    public static async Task PrefillAsync(DbContext db, EmployeeStatusPeriod period, DateOnly from, DateOnly to, Guid? userId,
        DateTimeOffset now, CancellationToken ct)
    {
        if (AttendanceOf(period.Status) is not { } status || to < from)
            return;
        var existing = await db.Set<AttendanceRecord>()
            .Where(a => a.EmployeeId == period.EmployeeId && a.WorkDate >= from && a.WorkDate <= to)
            .ToDictionaryAsync(a => a.WorkDate, ct);
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (!existing.TryGetValue(d, out var day))
                db.Add(day = new AttendanceRecord { EmployeeId = period.EmployeeId, WorkDate = d });
            day.Status = status;
            day.HoursWorked = null;
            day.OvertimeHours = 0;
            day.IsLocked = true;
            day.IsAutoAbsent = false;
            day.StatusPeriodId = period.Uid;
            day.RecordedBy = userId;
            day.RecordedAt = now;
        }
    }

    /// <summary>
    /// Removes the days a period wrote after <paramref name="after"/> (all of them when <c>null</c>). Loaded and removed one by
    /// one, not bulk-deleted, so each removal reaches <c>audit_log</c>.
    /// </summary>
    public static async Task UnfillAsync(DbContext db, EmployeeStatusPeriod period, DateOnly? after, CancellationToken ct) =>
        db.RemoveRange(await db.Set<AttendanceRecord>().Where(a => a.StatusPeriodId == period.Uid && (after == null || a.WorkDate > after))
            .ToListAsync(ct));

    /// <summary>The puesto's rates valid on <paramref name="date"/>.</summary>
    public static Task<PositionRate?> PositionRatesOn(DbContext db, Guid positionId, DateOnly date, CancellationToken ct) =>
        db.Set<PositionRate>().AsNoTracking()
            .Where(r => r.PositionId == positionId && r.ValidFrom <= date && (r.ValidTo == null || r.ValidTo >= date))
            .SingleOrDefaultAsync(ct);

    public static EmployeeRatesDto? Effective(Employee e, PositionRate? position) => position is null
        ? null
        : new EmployeeRatesDto(e.DailySalaryPocketOverride ?? position.DailySalaryPocket, e.DailySalaryImssOverride ?? position.DailySalaryImss,
            e.HourlyRateOverride ?? position.HourlyRate, e.DaysWorkedBonusOverride ?? position.DaysWorkedBonus);

    public static EmployeeRatesDto Rates(PositionRate r) => new(r.DailySalaryPocket, r.DailySalaryImss, r.HourlyRate, r.DaysWorkedBonus);

    /// <summary>The person has a current primary account, so no card needs to be requested.</summary>
    public static Task<bool> HasAccountAsync(DbContext db, Guid personId, DateOnly today, CancellationToken ct) =>
        db.Set<PersonBankAccount>().AnyAsync(a => a.PersonId == personId && a.IsPrimary && (a.ValidTo == null || a.ValidTo >= today), ct);

    /// <summary>Cancels the queued card requests of those employments (an account arrived, or a baja); audited one by one.</summary>
    public static async Task CancelQueuedCardsAsync(DbContext db, IQueryable<Guid> employeeIds, CancellationToken ct)
    {
        foreach (var r in await db.Set<CardRequest>().Where(r => r.Status == CardRequestStatus.Queued && employeeIds.Contains(r.EmployeeId))
                     .ToListAsync(ct))
            r.Status = CardRequestStatus.Cancelled;
    }
}
