using Microsoft.EntityFrameworkCore;
using Platform.Core.Catalogs;
using Platform.Payroll.Periods;

namespace Platform.Payroll.Prepayroll;

/// <summary>
/// A holiday was created, edited or deleted: every line of an open period (Capturando or Revisión) that covers one of the dates
/// is recomputed — HolidaysEntitled, DaysWorked and the FESTIVO amounts depend on the catalog (D84). Runs in the catalog write's
/// transaction and takes the same per-line locks as every other recompute.
/// </summary>
public sealed class HolidayChangeListener : IHolidayChangeListener
{
    private static readonly PeriodStatus[] Open = [PeriodStatus.Capturing, PeriodStatus.InReview];

    public async Task ChangedAsync(DbContext db, IReadOnlyCollection<DateOnly> dates, DateTimeOffset now, CancellationToken ct)
    {
        if (dates.Count == 0)
            return;
        var (first, last) = (dates.Min(), dates.Max());
        var periods = (await db.Set<PayrollPeriod>()
                .Where(p => Open.Contains(p.Status) && p.StartDate <= last && p.EndDate >= first).ToListAsync(ct))
            .Where(p => dates.Any(d => d >= p.StartDate && d <= p.EndDate))
            .ToList();
        // A whole period at a time (its data loaded once, its lines locked in one statement), periods in Guid order sorted in .NET:
        // the order every writer locks lines in, so a capture across groups at the same moment cannot deadlock with this.
        foreach (var period in periods.OrderBy(p => p.Uid))
            await Prepayrolls.EnsureLinesAsync(db, period, now, ct, recomputeAll: true);
        await db.SaveChangesAsync(ct);
    }
}
