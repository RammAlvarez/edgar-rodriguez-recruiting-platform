using Microsoft.EntityFrameworkCore;
using Platform.Core.Users;

namespace Platform.Recruiting;

/// <summary>Display names for timelines; an entry written in cover mode reads "Edgar (cubriendo a Mónica Soto)" (SPEC §4.4).</summary>
public static class UserNames
{
    public static async Task<Dictionary<Guid, string>> Of(DbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var set = ids.OfType<Guid>().Distinct().ToList();
        return set.Count == 0 ? [] : await db.Set<User>().Where(u => set.Contains(u.Uid)).ToDictionaryAsync(u => u.Uid, u => u.DisplayName, ct);
    }

    public static string? Author(IReadOnlyDictionary<Guid, string> names, Guid? userId, Guid? actingFor)
    {
        var author = userId is { } u ? names.GetValueOrDefault(u) : null;
        return actingFor is { } a && names.GetValueOrDefault(a) is { } covered ? $"{author} (cubriendo a {covered})" : author;
    }

    /// <summary>
    /// The author a client sees: an entry written in cover mode reads as the covered recruiter alone (owner, after S5) — the
    /// client's contact is the recruiter of the vacante, and who covered is internal.
    /// </summary>
    public static string? ClientAuthor(IReadOnlyDictionary<Guid, string> names, Guid? userId, Guid? actingFor) =>
        (actingFor is { } a ? names.GetValueOrDefault(a) : null) ?? (userId is { } u ? names.GetValueOrDefault(u) : null);
}
