using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Abstractions;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;

namespace Platform.Payroll.Periods;

/// <summary>Postgres enum <c>period_status</c>: no backward transitions (DATA_MODEL §5.10).</summary>
public enum PeriodStatus
{
    Capturing,
    InReview,
    Paid,
}

/// <summary>
/// DATA_MODEL §5.10, one payroll period of a group («SEMANA 38», 11–17 sep 2026). Created ahead by <c>period-roll</c>; moved to
/// <see cref="PeriodStatus.InReview"/> by <c>attendance-lock</c> when its last day locks; <see cref="PeriodStatus.Paid"/> is S10.
/// </summary>
public sealed class PayrollPeriod : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PayrollGroupId { get; set; }
    public int PeriodNumber { get; set; }
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PeriodStatus Status { get; set; } = PeriodStatus.Capturing;
    public DateTimeOffset? ReviewStartedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public Guid? PaidBy { get; set; }
    public DateOnly? DepositDate { get; set; }
    public decimal? TotalNet { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

internal sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> b)
    {
        b.ToTable("payroll_periods", t => t.HasCheckConstraint("ck_payroll_periods_dates", "end_date >= start_date"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(p => p.Status).HasDefaultValue(PeriodStatus.Capturing).HasSentinel(PeriodStatus.Capturing);
        b.Property(p => p.TotalNet).HasPrecision(14, 2);
        b.HasIndex(p => new { p.PayrollGroupId, p.Year, p.PeriodNumber }).IsUnique();
        b.HasIndex(p => new { p.PayrollGroupId, p.StartDate }).IsUnique();
        b.HasOne<PayrollGroup>().WithMany().HasForeignKey(p => p.PayrollGroupId).HasPrincipalKey(g => g.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.PaidBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Period boundaries (PLAN D47, D83): weekly periods end on the group's cut-off weekday (end − 6 … end), numbered by the ISO
/// week of the end date; biweekly periods are the calendar halves 1–15 and 16–end of month, numbered 1–24.
/// </summary>
public static class PeriodCalendar
{
    public static (DateOnly Start, DateOnly End, int Number, int Year) Containing(PayrollGroup group, DateOnly date)
    {
        if (group.PeriodType == PeriodType.Biweekly)
        {
            var (start, end) = date.Day <= 15
                ? (new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, 15))
                : (new DateOnly(date.Year, date.Month, 16), new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));
            return (start, end, PeriodNumbering.BiweeklyNumber(end), end.Year);
        }
        // 0 = domingo … 6 = sábado, as DayOfWeek.
        var ahead = ((int)group.CutoffWeekday - (int)date.DayOfWeek + 7) % 7;
        var weekEnd = date.AddDays(ahead);
        return (weekEnd.AddDays(-6), weekEnd, PeriodNumbering.WeeklyNumber(weekEnd), PeriodNumbering.WeeklyYear(weekEnd));
    }

    /// <summary>Creates the periods containing each date when missing (idempotent); returns how many were created.</summary>
    public static async Task<int> EnsureAsync(DbContext db, PayrollGroup group, IEnumerable<DateOnly> dates, CancellationToken ct)
    {
        var created = 0;
        foreach (var date in dates.Distinct())
        {
            var (start, end, number, year) = Containing(group, date);
            if (await db.Set<PayrollPeriod>().AnyAsync(p => p.PayrollGroupId == group.Uid && p.StartDate == start, ct)
                || db.ChangeTracker.Entries<PayrollPeriod>().Any(e => e.Entity.PayrollGroupId == group.Uid && e.Entity.StartDate == start))
                continue;
            db.Add(new PayrollPeriod { PayrollGroupId = group.Uid, StartDate = start, EndDate = end, PeriodNumber = number, Year = year });
            created++;
        }
        return created;
    }
}
