using Microsoft.EntityFrameworkCore;

namespace Platform.Core.Catalogs;

/// <summary>
/// Told, inside the transaction that saved them, which dates of the holiday catalog changed (created, edited, moved, deleted).
/// Payroll implements it: days are priced by the holiday list, so the open prenómina lines covering those dates are recomputed.
/// </summary>
public interface IHolidayChangeListener
{
    Task ChangedAsync(DbContext db, IReadOnlyCollection<DateOnly> dates, DateTimeOffset now, CancellationToken ct);
}
