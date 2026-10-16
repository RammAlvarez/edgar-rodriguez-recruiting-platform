using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting;

/// <summary>
/// DATA_MODEL §8 for Reclutamiento (PLAN D73): an Administrador with <c>recruiting</c> sees every vacante; a Reclutador — or an
/// Administrador covering one (<c>X-Acting-For</c>) — sees the vacantes where that recruiter is líder or apoyo, and their
/// candidates. The líder always has a <c>job_opening_recruiters</c> row (D71), so one join covers both roles.
/// </summary>
public static class RecruitingScope
{
    /// <summary>Recruiting routes Reclutadores share (their own scope); Administradores need <c>recruiting</c>.</summary>
    public static readonly AccessRule Staff =
        new(new HashSet<UserRole> { UserRole.Administrator, UserRole.Recruiter }, PermissionModule.Recruiting);

    /// <summary>Administrador-only recruiting routes: create and edit vacantes, assign recruiters (SPEC §4.8).</summary>
    public static readonly AccessRule Manage = AccessRule.Administrator(PermissionModule.Recruiting);

    /// <summary>The recruiter whose scope applies, or <c>null</c> for an Administrador outside cover mode (no limit).</summary>
    public static Guid? ScopedRecruiter(RequestContext ctx) =>
        ctx.ActingFor is null && ctx.RequiredPrincipal.IsAdministrator ? null : ctx.ScopeUserId;

    public static IQueryable<JobOpening> Openings(DbContext db, RequestContext ctx)
    {
        var recruiter = ScopedRecruiter(ctx);
        var q = db.Set<JobOpening>().AsQueryable();
        return recruiter is null ? q : q.Where(o => o.Recruiters.Any(r => r.UserId == recruiter));
    }

    public static IQueryable<Candidate> Candidates(DbContext db, RequestContext ctx)
    {
        var recruiter = ScopedRecruiter(ctx);
        var q = db.Set<Candidate>().AsQueryable();
        return recruiter is null
            ? q
            : q.Where(c => db.Set<JobOpeningRecruiter>().Any(r => r.JobOpeningId == c.JobOpeningId && r.UserId == recruiter));
    }

    public const string OpeningNotFound = "La vacante no existe o no está asignada a ti.";
    public const string CandidateNotFound = "El candidato no existe o no pertenece a tus vacantes.";
}
