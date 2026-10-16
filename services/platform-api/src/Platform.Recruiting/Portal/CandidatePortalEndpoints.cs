using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Portal;

/// <summary>
/// The Candidates app (SPEC §7, PLAN D77): the signed-in candidate's own profile, the aviso de privacidad, their applications
/// in friendly words and the document checklist of their current application. Uploads and views use the shared document
/// routes, which let a candidate reach only their own person.
/// </summary>
public static class CandidatePortalEndpoints
{
    public const string Tag = "candidate-portal";

    public const string DuplicateId =
        "Esa CURP o ese RFC ya están registrados con otra persona. Revisa que estén bien escritos o avisa a tu reclutador.";

    public static IEndpointRouteBuilder MapCandidatePortalEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/candidate-portal").WithTags("Candidate portal");

        g.MapGet("/profile", async (RequestContext ctx, DbContext db, CancellationToken ct) =>
                TypedResults.Ok(CandidateProfileDto.From(await Self(db, ctx, ct))))
            .WithName("GetCandidateProfile").RequireAccess(AccessRule.Candidate);

        g.MapPut("/profile", async (CandidateProfileRequest r, RequestContext ctx, DbContext db, CancellationToken ct) =>
            {
                var person = await db.Set<Person>().SingleAsync(p => p.Uid == ctx.Candidate!.PersonId, ct);
                PeopleEndpoints.Apply(person, r.ToPersonRequest(person));
                // Never name the other person to a candidate (the staff message does).
                if (await PeopleEndpoints.DuplicateConflict(db, person, ct) is not null)
                    return ApiResults.Conflict(DuplicateId);
                return await db.SaveOrConflictAsync(async () => TypedResults.Ok(CandidateProfileDto.From(await Self(db, ctx, ct))),
                    DuplicateId, ct);
            })
            .WithName("UpdateCandidateProfile").Validate<CandidateProfileRequest>().Produces<CandidateProfileDto>()
            .ProducesProblem(StatusCodes.Status409Conflict).RequireAccess(AccessRule.Candidate).Invalidates(Tag, PeopleEndpoints.Tag);

        g.MapPost("/privacy-notice", async (RequestContext ctx, DbContext db, IClock clock, CancellationToken ct) =>
            {
                var person = await db.Set<Person>().SingleAsync(p => p.Uid == ctx.Candidate!.PersonId, ct);
                person.PrivacyNoticeAcceptedAt ??= clock.UtcNow;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(CandidateProfileDto.From(person));
            })
            .WithName("AcceptPrivacyNotice").RequireAccess(AccessRule.Candidate).Invalidates(Tag);

        g.MapGet("/applications", async (RequestContext ctx, DbContext db, IClock clock, CancellationToken ct) =>
                TypedResults.Ok(await Applications(db, ctx.Candidate!.PersonId, clock, ct)))
            .WithName("ListCandidateApplications").RequireAccess(AccessRule.Candidate);

        g.MapGet("/documents", async (RequestContext ctx, DbContext db, IClock clock, CancellationToken ct) =>
            {
                var personId = ctx.Candidate!.PersonId;
                var current = (await Rows(db, personId, ct)).OrderBy(r => Outcome(r.Stage, r.Status) == ApplicationOutcome.Closed)
                    .ThenByDescending(r => r.CapturedAt).ThenByDescending(r => r.Id).FirstOrDefault();
                return await DocumentEndpoints.Checklist(db, personId, current?.TemplateId, ct) is { } list
                    ? TypedResults.Ok(list)
                    : ApiResults.NotFound(PeopleEndpoints.NotFound);
            })
            .WithName("GetCandidateDocuments").Produces<DocumentChecklistDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(AccessRule.Candidate);

        return v1;
    }

    private static Task<Person> Self(DbContext db, RequestContext ctx, CancellationToken ct) =>
        db.Set<Person>().AsNoTracking().SingleAsync(p => p.Uid == ctx.Candidate!.PersonId, ct);

    private sealed record Row(long Id, Guid Uid, Guid OpeningId, CandidateStage Stage, OpeningStatus Status, DateOnly CapturedAt,
        DateOnly? HiredAt, Guid? RecruiterId, Guid? TemplateId);

    private static Task<List<Row>> Rows(DbContext db, Guid personId, CancellationToken ct) =>
        db.Set<Candidate>().AsNoTracking().Where(c => c.PersonId == personId)
            .Join(db.Set<JobOpening>(), c => c.JobOpeningId, o => o.Uid, (c, o) => new Row(c.Id, c.Uid, o.Uid, c.Stage, o.Status,
                c.CapturedAt, c.HiredAt, c.RecruiterId ?? o.LeadRecruiterId, o.DocumentTemplateId))
            .ToListAsync(ct);

    public static async Task<List<CandidateApplicationDto>> Applications(DbContext db, Guid personId, IClock clock, CancellationToken ct)
    {
        var rows = await Rows(db, personId, ct);
        var names = await OpeningQueries.DisplayNames(db, rows.Select(r => r.OpeningId).Distinct().ToList(), ct);
        var recruiterIds = rows.Select(r => r.RecruiterId).OfType<Guid>().Distinct().ToList();
        var recruiters = await db.Set<User>().AsNoTracking().Where(u => recruiterIds.Contains(u.Uid))
            .ToDictionaryAsync(u => u.Uid, ct);
        var ids = rows.Select(r => r.Uid).ToList();
        var now = clock.UtcNow;
        var interviews = (await db.Set<Interview>().AsNoTracking()
                .Where(i => ids.Contains(i.CandidateId) && i.Outcome == InterviewOutcome.Pending && i.ScheduledAt >= now)
                .ToListAsync(ct))
            .GroupBy(i => i.CandidateId).ToDictionary(g => g.Key, g => g.MinBy(i => i.ScheduledAt)!);
        return rows.OrderBy(r => Outcome(r.Stage, r.Status) == ApplicationOutcome.Closed)
            .ThenByDescending(r => r.CapturedAt).ThenByDescending(r => r.Id)
            .Select(r =>
            {
                var recruiter = r.RecruiterId is { } id ? recruiters.GetValueOrDefault(id) : null;
                var outcome = Outcome(r.Stage, r.Status);
                var interview = outcome == ApplicationOutcome.InProcess ? interviews.GetValueOrDefault(r.Uid) : null;
                return new CandidateApplicationDto(r.Uid, names[r.OpeningId], Step(r.Stage, outcome), outcome, r.CapturedAt,
                    r.HiredAt, recruiter?.DisplayName, recruiter?.Phone, recruiter?.Email, interview?.ScheduledAt, interview?.Location);
            })
            .ToList();
    }

    public static ApplicationOutcome Outcome(CandidateStage stage, OpeningStatus opening) => stage switch
    {
        CandidateStage.Hired => ApplicationOutcome.Hired,
        CandidateStage.NotSuitable or CandidateStage.Discarded => ApplicationOutcome.Closed,
        _ when opening is OpeningStatus.Cancelled or OpeningStatus.Filled => ApplicationOutcome.Closed,
        CandidateStage.Suitable => ApplicationOutcome.Selected,
        _ => ApplicationOutcome.InProcess,
    };

    public static ApplicationStep Step(CandidateStage stage, ApplicationOutcome outcome) => outcome != ApplicationOutcome.InProcess
        ? ApplicationStep.Result
        : stage switch
        {
            CandidateStage.Contacted => ApplicationStep.Contacted,
            CandidateStage.Interview => ApplicationStep.Interview,
            _ => ApplicationStep.Received,
        };
}
