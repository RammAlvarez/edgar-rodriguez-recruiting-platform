using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.People;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting;

/// <summary>
/// <see cref="IPersonScope"/> for the recruiting side (DATA_MODEL §8, PLAN D73): an Administrador outside cover mode reaches
/// every person; a Reclutador — or an Administrador covering one — only people who are candidates of their vacantes; a
/// candidate only themselves (D77).
/// </summary>
public sealed class RecruiterPersonScope(DbContext db, RequestContext ctx) : IPersonScope
{
    public async Task<bool> CanAccessAsync(Guid personId, CancellationToken ct)
    {
        if (ctx.Candidate is { } candidate)
            return candidate.PersonId == personId;
        if (RecruitingScope.ScopedRecruiter(ctx) is not { } recruiter)
            return true;
        return await db.Set<Candidate>().AnyAsync(c => c.PersonId == personId
            && db.Set<JobOpeningRecruiter>().Any(r => r.JobOpeningId == c.JobOpeningId && r.UserId == recruiter), ct);
    }
}
