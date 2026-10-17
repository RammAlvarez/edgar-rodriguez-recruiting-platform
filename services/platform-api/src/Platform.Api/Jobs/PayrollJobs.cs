using Microsoft.EntityFrameworkCore;
using Platform.Api.Internal;
using Platform.Core.Abstractions;
using Platform.Core.Clients;
using Platform.Core.Notifications;
using Platform.Core.Users;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;
using Platform.Payroll.Staff;

namespace Platform.Api.Jobs;

/// <summary>
/// <c>attendance-lock</c>, every 15 min (ARCHITECTURE §4): locks the days whose lock time passed in each group's time zone,
/// and moves a period whose last day locked to <c>in_review</c>, writing a falta (flagged automatic) for every day an employee
/// worked in the group without a record (SPEC §5.3, PLAN D83). Idempotent.
/// </summary>
public sealed class AttendanceLockJob(DbContext db, IClock clock) : IJob
{
    public string Name => "attendance-lock";

    public async Task<object> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        int locked = 0, reviewed = 0, absences = 0, missing = 0;
        foreach (var group in await db.Set<PayrollGroup>().AsNoTracking().ToListAsync(cancellationToken))
        {
            var lastLocked = LastLockedDay(group, now);
            // One statement per group: the lock flag is the system's, not a user change (ARCHITECTURE §4 «Attendance lock»).
            locked += await db.Set<AttendanceRecord>()
                .Where(a => !a.IsLocked && a.WorkDate <= lastLocked && db.Set<Employee>().Any(e => e.Uid == a.EmployeeId && e.PayrollGroupId == group.Uid))
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.IsLocked, true), cancellationToken);

            missing += await NotifyMissingAsync(group, lastLocked, now, cancellationToken);

            var closing = await db.Set<PayrollPeriod>()
                .Where(p => p.PayrollGroupId == group.Uid && p.Status == PeriodStatus.Capturing && p.EndDate <= lastLocked)
                .OrderBy(p => p.StartDate).ToListAsync(cancellationToken);
            foreach (var period in closing)
            {
                // One transaction per period, as «Cerrar captura» does: the lines' advisory locks and the batch recompute need it.
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                absences += await Prepayrolls.CloseCaptureAsync(db, group, period, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                reviewed++;
            }
        }
        return new { locked, periodsInReview = reviewed, autoAbsences = absences, missingNotified = missing };
    }

    /// <summary>
    /// The day that just closed: people the Calendario expected and nobody captured are told, once per group and day, to
    /// whoever captures attendance for that client (SPEC §3.5 «asistencia no capturada al corte»).
    /// </summary>
    private async Task<int> NotifyMissingAsync(PayrollGroup group, DateOnly day, DateTimeOffset now, CancellationToken ct)
    {
        var key = $"attendance-missing:{group.Uid}:{day:yyyy-MM-dd}";
        if (await db.Set<Notification>().AnyAsync(n => n.DedupeKey == key, ct))
            return 0;
        var groupEmployees = db.Set<Employee>().Where(e => e.PayrollGroupId == group.Uid).Select(e => e.Uid);
        var expected = (await ScheduleEndpoints.Calendar(db, db.Set<PayrollGroup>().Where(g => g.Uid == group.Uid), day, day, group.ClientId, ct))
            .SelectMany(d => d.Sites).SelectMany(s => s.Entries).Select(e => e.EmployeeId).Distinct().ToList();
        var ids = await groupEmployees.Where(id => expected.Contains(id)).ToListAsync(ct);
        var captured = await db.Set<AttendanceRecord>().Where(a => ids.Contains(a.EmployeeId) && a.WorkDate == day).Select(a => a.EmployeeId).ToListAsync(ct);
        var count = ids.Except(captured).Count();
        if (count == 0)
            return 0;
        var client = await db.Set<Client>().Where(c => c.Uid == group.ClientId).Select(c => c.ShortName).SingleAsync(ct);
        var told = await Notifier.NotifyAsync(db, await Notifier.Holders(db, PermissionModule.PayrollAttendance, group.ClientId).ToListAsync(ct),
            new NotificationDraft(NotificationKind.AttendanceMissing, key, $"{client} · {group.Name}: {count} sin captura el {day:dd/MM/yyyy}",
                "El día ya cerró; esas personas quedarán con falta al cerrar el período si nadie lo corrige.", $"/asistencia?date={day:yyyy-MM-dd}"),
            now, ct);
        await db.SaveChangesAsync(ct);
        return told;
    }

    /// <summary>The last date whose lock moment has passed for the group.</summary>
    public static DateOnly LastLockedDay(PayrollGroup group, DateTimeOffset now)
    {
        var today = AttendanceRules.Today(group, now);
        return AttendanceRules.IsLocked(group, today, now) ? today : today.AddDays(-1);
    }
}

/// <summary><c>period-roll</c>, daily: makes sure each active group has the period containing today and the next one.</summary>
public sealed class PeriodRollJob(DbContext db, IClock clock) : IJob
{
    public string Name => "period-roll";

    public async Task<object> RunAsync(CancellationToken cancellationToken)
    {
        int created = 0, lines = 0;
        foreach (var group in await db.Set<PayrollGroup>().Where(g => g.IsActive).AsNoTracking().ToListAsync(cancellationToken))
        {
            var today = AttendanceRules.Today(group, clock.UtcNow);
            var current = PeriodCalendar.Containing(group, today);
            created += await PeriodCalendar.EnsureAsync(db, group, [today, current.End.AddDays(1)], cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            // Every open period has a line per employee who works in it (idempotent).
            foreach (var period in await db.Set<PayrollPeriod>()
                         .Where(p => p.PayrollGroupId == group.Uid && p.Status != PeriodStatus.Paid).ToListAsync(cancellationToken))
            {
                // In a transaction so the lines are locked against a capture at the same moment (D-013).
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                lines += await Prepayrolls.EnsureLinesAsync(db, period, clock.UtcNow, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        return new { periodsCreated = created, linesCreated = lines };
    }
}

/// <summary>
/// <c>status-roll</c>, daily: sets <c>employees.status</c> to the status of the period covering today in the group's zone, so an
/// expired vacation or incapacidad returns to Activo (DATA_MODEL §5.4b). Bajas stay bajas.
/// </summary>
public sealed class StatusRollJob(DbContext db, IClock clock) : IJob
{
    public string Name => "status-roll";

    public async Task<object> RunAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Set<Employee>().Where(e => e.TerminatedAt == null)
            .Join(db.Set<PayrollGroup>(), e => e.PayrollGroupId, g => g.Uid, (e, g) => new { e, g })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.e.Uid).ToList();
        var periods = await db.Set<EmployeeStatusPeriod>().AsNoTracking().Where(p => ids.Contains(p.EmployeeId)).ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var r in rows)
        {
            var status = StaffRules.StatusOn(periods.Where(p => p.EmployeeId == r.e.Uid), AttendanceRules.Today(r.g, clock.UtcNow));
            if (status == r.e.Status)
                continue;
            r.e.Status = status;
            changed++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return new { statusesChanged = changed };
    }
}
