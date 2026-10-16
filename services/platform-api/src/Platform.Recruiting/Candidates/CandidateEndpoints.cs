using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Candidates;

/// <summary>
/// Candidatos (SPEC §4.2–4.3): capture with the people registry, the stage machine (PLAN D72) that keeps the vacante's derived
/// status in the same transaction (D70), notes (D74), interviews and the Historial. Scoped by <see cref="RecruitingScope"/>.
/// </summary>
public static class CandidateEndpoints
{
    public const string Tag = "candidates";

    private static readonly string[] Touches = [Tag, OpeningEndpoints.Tag, OpeningEndpoints.RecruitersTag, PeopleEndpoints.Tag];

    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/candidates").WithTags("Candidates");

        g.MapGet("", List).WithName("ListCandidates").Produces<List<CandidateListItemDto>>().ProducesValidationProblem()
            .RequireAccess(RecruitingScope.Staff);
        g.MapGet("/{id:guid}", async (Guid id, DbContext db, RequestContext ctx, IClock clock, DataAccess log, CancellationToken ct) =>
            {
                if (await Detail(db, ctx, id, clock, ct) is not { } dto)
                    return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
                await log.RecordAsync(dto.Person.Id, DataAccessKind.ViewProfile, null, ct);
                return TypedResults.Ok(dto);
            })
            .WithName("GetCandidate").Produces<CandidateDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(RecruitingScope.Staff);

        g.MapPost("", Create).WithName("CreateCandidate").Validate<CreateCandidateRequest>().Produces<CandidateDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Touches)
            .RequireAccess(RecruitingScope.Staff);
        g.MapPut("/{id:guid}", Update).WithName("UpdateCandidate").Validate<UpdateCandidateRequest>().Produces<CandidateDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).Invalidates(Touches).RequireAccess(RecruitingScope.Staff);
        g.MapPost("/{id:guid}/stage", MoveStage).WithName("MoveCandidateStage").Validate<MoveStageRequest>()
            .Produces<MoveStageResultDto>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .Invalidates(Touches).RequireAccess(RecruitingScope.Staff);
        g.MapGet("/{id:guid}/history", History).WithName("GetCandidateHistory").Produces<List<TimelineEntryDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(RecruitingScope.Staff);

        CandidateNotes.Map(g);
        CandidateInterviews.Map(g);
        CandidateInvitation.Map(g);
        return v1;
    }

    private static async Task<IResult> List(Guid? openingId, string? stage, Guid? recruiterId, Guid? clientId, string? search,
        DbContext db, RequestContext ctx, IClock clock, CancellationToken ct)
    {
        if (!WireEnum.TryParse<CandidateStage>(stage, out var s))
            return ApiResults.Validation(new Dictionary<string, string[]> { ["stage"] = ["Etapa desconocida."] });
        var q = RecruitingScope.Candidates(db, ctx);
        if (openingId is { } o)
            q = q.Where(c => c.JobOpeningId == o);
        if (s is { } st)
            q = q.Where(c => c.Stage == st);
        if (recruiterId is { } r)
            q = q.Where(c => c.RecruiterId == r);
        if (clientId is { } cl)
            q = q.Where(c => db.Set<JobOpening>().Any(x => x.Uid == c.JobOpeningId && x.ClientId == cl));
        if (search.Clean() is { } text)
        {
            var id = PersonText.Id(text)!;
            var name = PersonText.Name(text);
            q = q.Where(c => db.Set<Person>().Any(p => p.Uid == c.PersonId
                && (p.Curp == id || p.MobilePhone == text || EF.Functions.ILike(p.FullName, $"%{name}%"))));
        }
        return TypedResults.Ok(await ListItems(db, q, clock, ct));
    }

    public static async Task<List<CandidateListItemDto>> ListItems(DbContext db, IQueryable<Candidate> q, IClock clock, CancellationToken ct)
    {
        var rows = await q.OrderByDescending(c => c.CapturedAt).ThenByDescending(c => c.Id)
            .Join(db.Set<Person>(), c => c.PersonId, p => p.Uid, (c, p) => new { c, p.FullName, p.MobilePhone })
            .Join(db.Set<JobOpening>(), x => x.c.JobOpeningId, o => o.Uid, (x, o) => new
            {
                x.c,
                x.FullName,
                x.MobilePhone,
                Client = db.Set<Client>().Where(cl => cl.Uid == o.ClientId).Select(cl => cl.ShortName).First(),
                Recruiter = db.Set<User>().Where(u => u.Uid == x.c.RecruiterId).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .AsNoTracking().Take(500).ToListAsync(ct);
        var names = await OpeningQueries.DisplayNames(db, rows.Select(r => r.c.JobOpeningId).Distinct().ToList(), ct);
        var today = clock.Today();
        return rows.Select(r => new CandidateListItemDto(r.c.Uid, r.c.PersonId, r.FullName, r.MobilePhone, r.c.JobOpeningId,
            names[r.c.JobOpeningId], r.Client, r.c.RecruiterId, r.Recruiter, r.c.Source, r.c.Stage, r.c.IsSuitable, r.c.IsHired,
            r.c.CapturedAt, r.c.HiredAt, r.c.DaysInProcess(today), r.c.OfferedSalary)).ToList();
    }

    public static async Task<CandidateDetailDto?> Detail(DbContext db, RequestContext ctx, Guid id, IClock clock, CancellationToken ct)
    {
        var c = await RecruitingScope.Candidates(db, ctx).AsNoTracking().SingleOrDefaultAsync(x => x.Uid == id, ct);
        if (c is null)
            return null;
        var person = await db.Set<Person>().AsNoTracking().SingleAsync(p => p.Uid == c.PersonId, ct);
        var o = await db.Set<JobOpening>().AsNoTracking().SingleAsync(x => x.Uid == c.JobOpeningId, ct);
        var client = await db.Set<Client>().Where(x => x.Uid == o.ClientId).Select(x => x.ShortName).SingleAsync(ct);
        var recruiter = c.RecruiterId is { } r
            ? await db.Set<User>().Where(u => u.Uid == r).Select(u => u.DisplayName).SingleOrDefaultAsync(ct)
            : null;
        var name = (await OpeningQueries.DisplayNames(db, [o.Uid], ct))[o.Uid];
        return new CandidateDetailDto(c.Uid, PersonDto.From(person), o.Uid, name, o.ClientId, client, o.Status, o.DocumentTemplateId,
            c.RecruiterId, recruiter, c.Source, c.Stage, c.IsSuitable, c.IsHired, c.CapturedAt, c.HiredAt, c.DaysInProcess(clock.Today()),
            c.OfferedSalary, c.Comments, c.DiscardReason,
            CanConvertToEmployee: c.Stage == CandidateStage.Hired && c.EmployeeId is null
                && ctx.RequiredPrincipal.HasGrant(Platform.Core.Users.PermissionModule.PayrollStaff),
            c.EmployeeId);
    }

    private static async Task<IResult> Create(CreateCandidateRequest r, DbContext db, RequestContext ctx, IClock clock,
        IEmailSender email, AppLinks links, ILoggerFactory logs, CancellationToken ct)
    {
        if (await RecruitingScope.Openings(db, ctx).SingleOrDefaultAsync(o => o.Uid == r.JobOpeningId, ct) is not { } opening)
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        if (!OpeningRules.Active.Contains(opening.Status))
            return ApiResults.Conflict(CandidateMessages.OpeningClosed(opening.Status));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Guid personId;
        if (r.PersonId is { } existing)
        {
            if (!await db.Set<Person>().AnyAsync(p => p.Uid == existing && p.DeletedAt == null, ct))
                return ApiResults.NotFound(PeopleEndpoints.NotFound);
            personId = existing;
        }
        else
        {
            var person = new Person { FirstNames = "", LastNamePaternal = "" };
            PeopleEndpoints.Apply(person, r.Person!);
            person.Source ??= r.Source;
            if (await PeopleEndpoints.DuplicateConflict(db, person, ct) is { } conflict)
                return conflict;
            db.Add(person);
            personId = person.Uid;
        }
        if (await db.Set<Candidate>().AnyAsync(c => c.PersonId == personId && c.JobOpeningId == opening.Uid, ct))
            return ApiResults.Conflict("Esta persona ya es candidata de esta vacante.");

        var candidate = new Candidate
        {
            CapturedAt = r.CapturedAt ?? clock.Today(),
            PersonId = personId,
            JobOpeningId = opening.Uid,
            // D72: the covered recruiter in cover mode, the Reclutador herself, otherwise the vacante's líder.
            RecruiterId = ctx.ActingFor?.UserId
                ?? (ctx.RequiredPrincipal.Role == UserRole.Recruiter ? ctx.RequiredPrincipal.UserId : opening.LeadRecruiterId),
            Source = r.Source,
            OfferedSalary = r.OfferedSalary,
            Comments = r.Comments.Clean(),
        };
        db.Add(candidate);
        db.Add(StageChange(ctx, clock, candidate, null, CandidateStage.New, null));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.UniqueViolation(e) is not null)
        {
            return ApiResults.Conflict("Esta persona ya es candidata de esta vacante, o su CURP o RFC ya está registrada.");
        }
        await OpeningRules.RecomputeAsync(db, opening, ctx, clock, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // SPEC §4.3 Nuevo: the invitation to the Candidates app, after the commit (D77).
        await CandidateInvitation.SendAsync(db, email, links, logs, candidate.Uid, ct);
        return TypedResults.Ok(await Detail(db, ctx, candidate.Uid, clock, ct));
    }

    private static async Task<IResult> Update(Guid id, UpdateCandidateRequest r, DbContext db, RequestContext ctx, IClock clock,
        CancellationToken ct)
    {
        if (await RecruitingScope.Candidates(db, ctx).SingleOrDefaultAsync(c => c.Uid == id, ct) is not { } c)
            return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
        c.Source = r.Source;
        c.OfferedSalary = r.OfferedSalary;
        c.Comments = r.Comments.Clean();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Detail(db, ctx, id, clock, ct));
    }

    private static async Task<IResult> MoveStage(Guid id, MoveStageRequest r, DbContext db, RequestContext ctx, IClock clock,
        CancellationToken ct)
    {
        if (await RecruitingScope.Candidates(db, ctx).SingleOrDefaultAsync(c => c.Uid == id, ct) is not { } c)
            return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
        var opening = await db.Set<JobOpening>().SingleAsync(o => o.Uid == c.JobOpeningId, ct);
        if (r.Stage == c.Stage)
            return ApiResults.Conflict(CandidateMessages.SameStage);
        if (opening.Status is OpeningStatus.Paused or OpeningStatus.Cancelled)
            return ApiResults.Conflict(CandidateMessages.OpeningClosed(opening.Status));
        if (c.Stage == CandidateStage.Hired && c.EmployeeId is not null)
            return ApiResults.Conflict(CandidateMessages.Converted);
        if (r.Stage == CandidateStage.Hired)
        {
            if (r.HiredAt < c.CapturedAt)
                return ApiResults.Validation(new Dictionary<string, string[]> { ["hiredAt"] = [CandidateMessages.HiredBeforeCapture] });
            var hired = await db.Set<Candidate>().CountAsync(x => x.JobOpeningId == opening.Uid && x.IsHired && x.Uid != c.Uid, ct);
            if (hired >= opening.HeadcountRequested)
                return ApiResults.Conflict(CandidateMessages.HeadcountReached(opening.HeadcountRequested));
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reason = r.Stage is CandidateStage.NotSuitable or CandidateStage.Discarded ? r.Reason.Clean() : null;
        db.Add(StageChange(ctx, clock, c, c.Stage, r.Stage, reason));
        c.Stage = r.Stage;
        c.IsSuitable = r.Stage switch
        {
            CandidateStage.Suitable or CandidateStage.Hired => true,
            CandidateStage.NotSuitable => false,
            _ => null,
        };
        c.IsHired = r.Stage == CandidateStage.Hired;
        c.HiredAt = c.IsHired ? r.HiredAt : null;
        c.DiscardReason = reason;
        await db.SaveChangesAsync(ct);
        await OpeningRules.RecomputeAsync(db, opening, ctx, clock, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var detail = (await Detail(db, ctx, id, clock, ct))!;
        var hiredNow = await db.Set<Candidate>().CountAsync(x => x.JobOpeningId == opening.Uid && x.IsHired, ct);
        return TypedResults.Ok(new MoveStageResultDto(detail, detail.OpeningName, hiredNow, opening.HeadcountRequested, opening.Status));
    }

    private static CandidateStageChange StageChange(RequestContext ctx, IClock clock, Candidate c, CandidateStage? from,
        CandidateStage to, string? note) => new()
        {
            CandidateId = c.Uid,
            FromStage = from,
            ToStage = to,
            ChangedBy = ctx.RequiredPrincipal.UserId,
            ActingForUserId = ctx.ActingFor?.UserId,
            ChangedAt = clock.UtcNow,
            Note = note,
        };

    private static async Task<IResult> History(Guid id, DbContext db, RequestContext ctx, CancellationToken ct)
    {
        if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == id, ct))
            return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
        var stages = await db.Set<CandidateStageChange>().Where(h => h.CandidateId == id).AsNoTracking().ToListAsync(ct);
        var notes = await db.Set<CandidateNote>().Where(n => n.CandidateId == id && n.DeletedAt == null).AsNoTracking().ToListAsync(ct);
        var interviews = await db.Set<Interview>().Where(i => i.CandidateId == id).AsNoTracking().ToListAsync(ct);
        var names = await UserNames.Of(db, stages.SelectMany(h => new[] { h.ChangedBy, h.ActingForUserId })
            .Concat(notes.SelectMany(n => new Guid?[] { n.AuthorUserId, n.ActingForUserId }))
            .Concat(interviews.SelectMany(i => new[] { i.CreatedBy, i.ActingForUserId })), ct);
        var entries = stages.Select(h => new TimelineEntryDto(TimelineKind.Stage, h.FromStage, h.ToStage, h.Note,
                UserNames.Author(names, h.ChangedBy, h.ActingForUserId), h.ChangedAt))
            .Concat(notes.Select(n => new TimelineEntryDto(TimelineKind.Note, null, null, "Nota agregada",
                UserNames.Author(names, n.AuthorUserId, n.ActingForUserId), n.CreatedAt)))
            .Concat(interviews.Select(i => new TimelineEntryDto(TimelineKind.Interview, null, null, "Entrevista programada",
                UserNames.Author(names, i.CreatedBy, i.ActingForUserId), i.CreatedAt)))
            .OrderByDescending(e => e.At).ToList();
        return TypedResults.Ok(entries);
    }
}
