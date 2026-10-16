using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Users;

namespace Platform.Recruiting.Candidates;

/// <summary>«Entrevistas» of the candidate detail (DATA_MODEL §4.5, PLAN D75). The entrevistador is any active staff user.</summary>
internal static class CandidateInterviews
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/{id:guid}/interviews", async (Guid id, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == id, ct))
                    return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
                var rows = await db.Set<Interview>().Where(i => i.CandidateId == id).OrderByDescending(i => i.ScheduledAt)
                    .AsNoTracking().ToListAsync(ct);
                var names = await UserNames.Of(db, rows.Select(i => i.InterviewerId), ct);
                return TypedResults.Ok(rows.Select(i => Dto(i, names)).ToList());
            })
            .WithName("ListInterviews").Produces<List<InterviewDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(RecruitingScope.Staff);

        g.MapPost("/{id:guid}/interviews", async (Guid id, InterviewRequest r, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == id, ct))
                    return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
                if (await InterviewerProblem(db, r, ct) is { } problem)
                    return problem;
                var interview = new Interview { CandidateId = id, ActingForUserId = ctx.ActingFor?.UserId };
                Apply(interview, r);
                db.Add(interview);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(Dto(interview, await UserNames.Of(db, [interview.InterviewerId], ct)));
            })
            .WithName("CreateInterview").Validate<InterviewRequest>().Produces<InterviewDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).Invalidates(CandidateEndpoints.Tag).RequireAccess(RecruitingScope.Staff);

        g.MapPut("/{id:guid}/interviews/{interviewId:guid}", async (Guid id, Guid interviewId, InterviewRequest r, DbContext db,
                RequestContext ctx, CancellationToken ct) =>
            {
                if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == id, ct)
                    || await db.Set<Interview>().SingleOrDefaultAsync(i => i.Uid == interviewId && i.CandidateId == id, ct) is not { } interview)
                    return ApiResults.NotFound("La entrevista no existe.");
                if (await InterviewerProblem(db, r, ct) is { } problem)
                    return problem;
                Apply(interview, r);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(Dto(interview, await UserNames.Of(db, [interview.InterviewerId], ct)));
            })
            .WithName("UpdateInterview").Validate<InterviewRequest>().Produces<InterviewDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).Invalidates(CandidateEndpoints.Tag).RequireAccess(RecruitingScope.Staff);
    }

    private static async Task<IResult?> InterviewerProblem(DbContext db, InterviewRequest r, CancellationToken ct) =>
        r.InterviewerId is { } who && !await db.Set<User>().AnyAsync(u => u.Uid == who && u.IsActive
            && (u.Role == UserRole.Administrator || u.Role == UserRole.Recruiter), ct)
            ? ApiResults.Validation(new Dictionary<string, string[]> { ["interviewerId"] = ["Elige un entrevistador del equipo."] })
            : null;

    private static void Apply(Interview i, InterviewRequest r)
    {
        i.ScheduledAt = r.ScheduledAt;
        i.InterviewerId = r.InterviewerId;
        i.Location = r.Location.Clean();
        i.Outcome = r.Outcome;
        i.Notes = r.Notes.Clean();
    }

    private static InterviewDto Dto(Interview i, IReadOnlyDictionary<Guid, string> names) =>
        new(i.Uid, i.ScheduledAt, i.InterviewerId, i.InterviewerId is { } who ? names.GetValueOrDefault(who) : null, i.Location,
            i.Outcome, i.Notes);
}
