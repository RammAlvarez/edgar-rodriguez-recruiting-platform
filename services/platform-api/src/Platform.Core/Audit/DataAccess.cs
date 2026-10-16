using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;

namespace Platform.Core.Audit;

/// <summary>Writes <c>data_access_log</c> (DATA_MODEL §7) when personal data is read: who, for whom, which person/document.</summary>
public sealed class DataAccess(DbContext db, RequestContext request, IClock clock)
{
    public async Task RecordAsync(Guid personId, DataAccessKind access, Guid? documentId, CancellationToken ct)
    {
        db.Add(new DataAccessEntry
        {
            OccurredAt = clock.UtcNow,
            UserId = request.Principal?.UserId,
            ActingForUserId = request.ActingFor?.UserId,
            PersonId = personId,
            DocumentId = documentId,
            Access = access,
            RequestId = request.RequestId,
        });
        await db.SaveChangesAsync(ct);
    }
}
