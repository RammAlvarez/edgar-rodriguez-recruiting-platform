using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Recruiters;

/// <summary>KPIs of a recruiter card; the UI derives % conversión = contratados / presentados (SPEC §4.4, report 1).</summary>
public sealed record RecruiterSummaryDto(Guid Id, string DisplayName, string Email, string? Phone, bool IsActive,
    int OpeningsAssigned, int CandidatesPresented, int CandidatesSuitable, int CandidatesHired);

public sealed record RecruiterAssignmentDto(Guid OpeningId, string DisplayName, bool IsLead, DateOnly AssignedAt, OpeningStatus Status,
    int DaysOpen, int Candidates);

public sealed record RecruiterDetailDto(RecruiterSummaryDto Recruiter, IReadOnlyList<RecruiterAssignmentDto> Assignments);

/// <summary>An option of the entrevistador select: active Administradores and Reclutadores.</summary>
public sealed record StaffOptionDto(Guid Id, string DisplayName, UserRole Role);

/// <summary>Reclutamiento → Reclutadores (SPEC §4.4): cards, detail, «Asignar vacante» (Administrador with <c>recruiting</c>).</summary>
public static class RecruiterEndpoints
{
    public static IEndpointRouteBuilder MapRecruiterEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/recruiters").WithTags("Recruiters");

        g.MapGet("", async (DbContext db, CancellationToken ct) =>
                TypedResults.Ok(await Summaries(db, db.Set<User>().Where(u => u.Role == UserRole.Recruiter), ct)))
            .WithName("ListRecruiters").RequireAccess(RecruitingScope.Manage);

        g.MapGet("/{id:guid}", (Guid id, DbContext db, IClock clock, CancellationToken ct) => Detail(db, id, clock, ct))
            .WithName("GetRecruiter").Produces<RecruiterDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(RecruitingScope.Manage);

        g.MapPost("/{id:guid}/openings", async (Guid id, AssignOpeningsRequest r, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                if (!await OpeningRules.IsActiveRecruiter(db, id, ct))
                    return ApiResults.NotFound("El reclutador no existe o está inactivo.");
                var ids = r.OpeningIds.Distinct().ToList();
                var openings = await db.Set<JobOpening>().Include(o => o.Recruiters).Where(o => ids.Contains(o.Uid)).ToListAsync(ct);
                if (openings.Count != ids.Count)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["openingIds"] = ["Una de las vacantes no existe."] });
                if (openings.FirstOrDefault(o => !OpeningRules.Active.Contains(o.Status)) is not null)
                    return ApiResults.Conflict("Solo se asignan vacantes abiertas o en proceso.");
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                foreach (var o in openings)
                {
                    if (r.Role == AssignmentRole.Lead)
                        await OpeningRules.MakeLeadAsync(db, ctx, clock, o, id, ct);
                    else
                        OpeningRules.Assign(db, ctx, clock, o, id);
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return await Detail(db, id, clock, ct);
            })
            .WithName("AssignOpenings").Validate<AssignOpeningsRequest>().Produces<RecruiterDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .Invalidates(OpeningEndpoints.Tag, OpeningEndpoints.RecruitersTag, ClientEndpoints.Tag).RequireAccess(RecruitingScope.Manage);

        v1.MapGet("/staff", async (DbContext db, CancellationToken ct) => TypedResults.Ok(await db.Set<User>()
                .Where(u => u.IsActive && (u.Role == UserRole.Administrator || u.Role == UserRole.Recruiter))
                .OrderBy(u => u.DisplayName).Select(u => new StaffOptionDto(u.Uid, u.DisplayName, u.Role)).ToListAsync(ct)))
            .WithTags("Recruiters").WithName("ListStaff").RequireAccess(RecruitingScope.Staff);

        return v1;
    }

    private static async Task<List<RecruiterSummaryDto>> Summaries(DbContext db, IQueryable<User> users, CancellationToken ct) =>
        await users.OrderBy(u => u.DisplayName).Select(u => new RecruiterSummaryDto(u.Uid, u.DisplayName, u.Email, u.Phone, u.IsActive,
                db.Set<JobOpeningRecruiter>().Count(r => r.UserId == u.Uid),
                db.Set<Candidate>().Count(c => c.RecruiterId == u.Uid),
                db.Set<Candidate>().Count(c => c.RecruiterId == u.Uid && c.IsSuitable == true),
                db.Set<Candidate>().Count(c => c.RecruiterId == u.Uid && c.IsHired)))
            .ToListAsync(ct);

    private static async Task<IResult> Detail(DbContext db, Guid id, IClock clock, CancellationToken ct)
    {
        if ((await Summaries(db, db.Set<User>().Where(u => u.Uid == id && u.Role == UserRole.Recruiter), ct)).SingleOrDefault()
            is not { } summary)
            return ApiResults.NotFound("El reclutador no existe.");
        var rows = await db.Set<JobOpeningRecruiter>().Where(r => r.UserId == id)
            .Join(db.Set<JobOpening>(), r => r.JobOpeningId, o => o.Uid, (r, o) => new
            {
                r.IsLead,
                r.AssignedAt,
                Opening = o,
                Candidates = db.Set<Candidate>().Count(c => c.JobOpeningId == o.Uid),
            })
            .AsNoTracking().ToListAsync(ct);
        var names = await OpeningQueries.DisplayNames(db, rows.Select(r => r.Opening.Uid).ToList(), ct);
        var today = clock.Today();
        return TypedResults.Ok(new RecruiterDetailDto(summary, rows
            .OrderByDescending(r => r.AssignedAt).ThenBy(r => names[r.Opening.Uid], StringComparer.CurrentCulture)
            .Select(r => new RecruiterAssignmentDto(r.Opening.Uid, names[r.Opening.Uid], r.IsLead, r.AssignedAt, r.Opening.Status,
                r.Opening.DaysOpen(today), r.Candidates)).ToList()));
    }
}
