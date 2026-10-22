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
        var periods = (await db.Set<PayrollPeriod>().AsNoTracking()
                .Where(p => Open.Contains(p.Status) && p.StartDate <= last && p.EndDate >= first)
                .Select(p => new { p.Uid, p.StartDate, p.EndDate }).ToListAsync(ct))
            .Where(p => dates.Any(d => d >= p.StartDate && d <= p.EndDate))
            .Select(p => p.Uid).ToList();
        var lines = await db.Set<PrepayrollLine>().AsNoTracking()
            .Where(l => periods.Contains(l.PayrollPeriodId)).Select(l => new { l.PayrollPeriodId, l.EmployeeId }).ToListAsync(ct);
        await Prepayrolls.SaveAndRecomputeLinesAsync(db, lines.Select(l => (l.PayrollPeriodId, l.EmployeeId)), now, ct);
    }
}
