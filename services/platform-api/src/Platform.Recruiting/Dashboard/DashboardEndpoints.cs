using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Catalogs;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Users;

namespace Platform.Recruiting.Dashboard;

/// <summary>A current document of a person that expires within the window.</summary>
public sealed record ExpiringDocumentDto(Guid DocumentId, Guid PersonId, string FullName, string DocumentTypeName, DateOnly ExpiresAt);

/// <summary>
/// Inicio (SPEC §4, PLAN D69): the counts come from the lists the screens already read; the documents about to expire have no
/// list of their own, so this is the one read the dashboard adds. Administrador with Reclutamiento, like Inicio itself.
/// </summary>
public static class DashboardEndpoints
{
    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.Recruiting);

    /// <summary>The same 30-day window as the document-expiry notifications.</summary>
    public const int WindowDays = 30;

    public const int Limit = 10;

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/dashboard").WithTags("Dashboard");

        g.MapGet("/expiring-documents", async (DbContext db, IClock clock, CancellationToken ct) =>
            {
                var today = clock.Today();
                var until = today.AddDays(WindowDays);
                var rows = await db.Set<PersonDocument>().AsNoTracking()
                    .Where(d => d.SupersededBy == null && d.Status != DocumentStatus.Rejected && d.ExpiresAt != null && d.ExpiresAt >= today
                        && d.ExpiresAt <= until)
                    .Join(db.Set<DocumentType>(), d => d.DocumentTypeId, t => t.Uid, (d, t) => new { d.Uid, d.PersonId, d.ExpiresAt, Type = t.NameEs })
                    .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.Uid, x.PersonId, p.FullName, x.Type, x.ExpiresAt })
                    .OrderBy(x => x.ExpiresAt).ThenBy(x => x.FullName)
                    .Take(Limit)
                    .ToListAsync(ct);
                return TypedResults.Ok(rows.Select(x => new ExpiringDocumentDto(x.Uid, x.PersonId, x.FullName, x.Type, x.ExpiresAt!.Value)).ToList());
            })
            .WithName("GetExpiringDocuments").RequireAccess(Access);

        return v1;
    }
}
