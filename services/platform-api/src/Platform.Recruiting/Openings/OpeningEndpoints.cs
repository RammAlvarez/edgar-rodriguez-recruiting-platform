using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.Users;

namespace Platform.Recruiting.Openings;

/// <summary>
/// Vacantes (SPEC §4.1). Everyone on the recruiting side reads within their scope (<see cref="RecruitingScope"/>); only
/// Administradores create, edit, pause/cancel and assign (SPEC §4.8). The status is never written by the client (PLAN D70).
/// </summary>
public static class OpeningEndpoints
{
    public const string Tag = "openings";
    public const string RecruitersTag = "recruiters";

    public static IEndpointRouteBuilder MapOpeningEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/openings").WithTags("Openings");

        g.MapGet("", async (Guid? clientId, string? status, Guid? recruiterId, string? priority, Guid? zoneId,
                DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (!WireEnum.TryParse<OpeningStatus>(status, out var s) || !WireEnum.TryParse<OpeningPriority>(priority, out var p))
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["status"] = ["Filtro desconocido."] });
                var q = RecruitingScope.Openings(db, ctx);
                if (clientId is { } c)
                    q = q.Where(o => o.ClientId == c);
                if (s is { } st)
                    q = q.Where(o => o.Status == st);
                if (recruiterId is { } r)
                    q = q.Where(o => o.Recruiters.Any(x => x.UserId == r));
                if (p is { } pr)
                    q = q.Where(o => o.Priority == pr);
                if (zoneId is { } z)
                    q = q.Where(o => o.ZoneId == z);
                return Results.Ok(await OpeningQueries.List(db, q, clock, ct));
            })
            .WithName("ListOpenings").Produces<List<OpeningListItemDto>>().ProducesValidationProblem().RequireAccess(RecruitingScope.Staff);

        g.MapGet("/options", async (DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var rows = await RecruitingScope.Openings(db, ctx).Where(o => OpeningRules.Active.Contains(o.Status))
                    .Select(o => new
                    {
                        o.Uid,
                        o.HeadcountRequested,
                        Hired = db.Set<Candidates.Candidate>().Count(c => c.JobOpeningId == o.Uid && c.IsHired),
                        Lead = db.Set<User>().Where(u => u.Uid == o.LeadRecruiterId).Select(u => u.DisplayName).FirstOrDefault(),
                    })
                    .ToListAsync(ct);
                var names = await OpeningQueries.DisplayNames(db, rows.Select(r => r.Uid).ToList(), ct);
                return TypedResults.Ok(rows.Select(r => new OpeningOptionDto(r.Uid, names[r.Uid], r.HeadcountRequested, r.Hired, r.Lead))
                    .OrderBy(o => o.DisplayName, StringComparer.CurrentCulture).ToList());
            })
            .WithName("ListOpeningOptions").RequireAccess(RecruitingScope.Staff);

        g.MapGet("/{id:guid}", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                await OpeningQueries.Detail(db, RecruitingScope.Openings(db, ctx), id, clock, ct) is { } dto
                    ? TypedResults.Ok(dto)
                    : ApiResults.NotFound(RecruitingScope.OpeningNotFound))
            .WithName("GetOpening").Produces<OpeningDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(RecruitingScope.Staff);

        g.MapPost("", Create).WithName("CreateOpening").Mutation<OpeningRequest>().RequireAccess(RecruitingScope.Manage);
        g.MapPut("/{id:guid}", Update).WithName("UpdateOpening").Mutation<OpeningRequest>().RequireAccess(RecruitingScope.Manage);
        g.MapPost("/{id:guid}/status", ChangeStatus).WithName("ChangeOpeningStatus").Mutation<OpeningStatusRequest>()
            .RequireAccess(RecruitingScope.Manage);
        g.MapPost("/{id:guid}/duplicate", Duplicate).WithName("DuplicateOpening").Produces<OpeningDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).Invalidates(Tag, RecruitersTag, ClientEndpoints.Tag)
            .RequireAccess(RecruitingScope.Manage);

        g.MapPost("/{id:guid}/recruiters", AssignRecruiters).WithName("AssignRecruiters").Mutation<AssignRecruitersRequest>()
            .RequireAccess(RecruitingScope.Manage);
        g.MapPost("/{id:guid}/recruiters/{userId:guid}/lead", async (Guid id, Guid userId, DbContext db, RequestContext ctx,
                IClock clock, CancellationToken ct) =>
            {
                if (await Load(db, id, ct) is not { } o)
                    return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
                if (o.Recruiters.All(r => r.UserId != userId))
                    return ApiResults.NotFound("Ese reclutador no está asignado a la vacante.");
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await OpeningRules.MakeLeadAsync(db, ctx, clock, o, userId, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(await OpeningQueries.Detail(db, db.Set<JobOpening>(), id, clock, ct));
            })
            .WithName("MakeLeadRecruiter").Produces<OpeningDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(Tag, RecruitersTag).RequireAccess(RecruitingScope.Manage);
        g.MapPost("/{id:guid}/recruiters/{userId:guid}/remove", async (Guid id, Guid userId, RemoveRecruiterRequest r, DbContext db,
                RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Load(db, id, ct) is not { } o)
                    return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (await OpeningRules.RemoveAsync(db, ctx, clock, o, userId, r.NewLeadId, ct) is { } problem)
                    return problem;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return TypedResults.Ok(await OpeningQueries.Detail(db, db.Set<JobOpening>(), id, clock, ct));
            })
            .WithName("RemoveRecruiter").Mutation<RemoveRecruiterRequest>().RequireAccess(RecruitingScope.Manage);

        g.MapGet("/{id:guid}/events", Events).WithName("ListOpeningEvents").RequireAccess(RecruitingScope.Staff);
        g.MapGet("/{id:guid}/comments", Comments).WithName("ListOpeningComments").RequireAccess(RecruitingScope.Staff);
        g.MapPost("/{id:guid}/comments", PostComment).WithName("PostOpeningComment").Validate<OpeningCommentRequest>()
            .Produces<OpeningCommentDto>().ProducesProblem(StatusCodes.Status404NotFound).Invalidates(Tag)
            .RequireAccess(RecruitingScope.Staff);

        return v1;
    }

    private static RouteHandlerBuilder Mutation<TBody>(this RouteHandlerBuilder route) where TBody : class =>
        route.Validate<TBody>().Produces<OpeningDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, RecruitersTag, ClientEndpoints.Tag);

    private static Task<JobOpening?> Load(DbContext db, Guid id, CancellationToken ct) =>
        db.Set<JobOpening>().Include(o => o.Recruiters).SingleOrDefaultAsync(o => o.Uid == id, ct);

    private static async Task<IResult> Create(OpeningRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct)
    {
        if (await OpeningRules.ReferenceProblems(db, r, ct) is { } problem)
            return problem;
        var o = new JobOpening { PositionName = "", RequestedAt = r.RequestedAt ?? clock.Today() };
        OpeningRules.Apply(o, r);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Add(o);
        OpeningRules.AddEvent(db, ctx, clock, o, OpeningEventKind.Created, to: OpeningStatus.Open);
        // SPEC §3.3: a client becomes a recruiting client with its first vacante.
        var client = await db.Set<Client>().SingleAsync(c => c.Uid == r.ClientId, ct);
        client.UsesRecruiting = true;
        await db.SaveChangesAsync(ct);
        if (r.LeadRecruiterId is { } lead)
        {
            await OpeningRules.MakeLeadAsync(db, ctx, clock, o, lead, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return TypedResults.Ok(await OpeningQueries.Detail(db, db.Set<JobOpening>(), o.Uid, clock, ct));
    }

    private static async Task<IResult> Update(Guid id, OpeningRequest r, DbContext db, RequestContext ctx, IClock clock,
        CancellationToken ct)
    {
        if (await Load(db, id, ct) is not { } o)
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        if (await OpeningRules.ReferenceProblems(db, r, ct) is { } problem)
            return problem;
        if (r.ClientId != o.ClientId && await db.Set<Candidates.Candidate>().AnyAsync(c => c.JobOpeningId == o.Uid, ct))
            return ApiResults.Conflict("La vacante ya tiene candidatos; no se puede cambiar de cliente.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        OpeningRules.Apply(o, r);
        if (r.RequestedAt is { } requested)
            o.RequestedAt = requested;
        if (db.Entry(o).Properties.Any(p => p.IsModified))
            OpeningRules.AddEvent(db, ctx, clock, o, OpeningEventKind.Edited);
        await db.SaveChangesAsync(ct);
        // The líder is changed here or with «Hacer líder»; clearing it is «Quitar» (PLAN D71).
        if (r.LeadRecruiterId is { } lead && lead != o.LeadRecruiterId)
            await OpeningRules.MakeLeadAsync(db, ctx, clock, o, lead, ct);
        await OpeningRules.RecomputeAsync(db, o, ctx, clock, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Ok(await OpeningQueries.Detail(db, db.Set<JobOpening>(), o.Uid, clock, ct));
    }

    private static async Task<IResult> ChangeStatus(Guid id, OpeningStatusRequest r, DbContext db, RequestContext ctx, IClock clock,
        CancellationToken ct)
    {
        if (await Load(db, id, ct) is not { } o)
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        var from = o.Status;
        var allowed = r.Status switch
        {
            OpeningStatus.Paused => OpeningRules.Active.Contains(from),
            OpeningStatus.Cancelled => OpeningRules.Active.Contains(from) || from == OpeningStatus.Paused,
            _ => from is OpeningStatus.Paused or OpeningStatus.Cancelled,
        };
        if (!allowed)
            return ApiResults.Conflict(r.Status == OpeningStatus.Open
                ? "La vacante no está pausada ni cancelada."
                : from == OpeningStatus.Filled
                    ? "La vacante ya está cubierta."
                    : "La vacante ya tiene ese estatus.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (r.Status == OpeningStatus.Open)
        {
            // Reopen into the derived status; the reason, if any, goes on that entry.
            o.Status = OpeningStatus.Open;
            var counts = await db.Set<Candidates.Candidate>().Where(c => c.JobOpeningId == o.Uid)
                .GroupBy(_ => 1).Select(g => new { Total = g.Count(), Hired = g.Count(c => c.IsHired) }).SingleOrDefaultAsync(ct);
            var target = (counts?.Hired ?? 0) >= o.HeadcountRequested ? OpeningStatus.Filled
                : (counts?.Total ?? 0) > 0 ? OpeningStatus.InProgress : OpeningStatus.Open;
            o.Status = target;
            o.FilledAt = target == OpeningStatus.Filled ? clock.Today() : null;
            OpeningRules.AddEvent(db, ctx, clock, o, OpeningEventKind.StatusChanged, from, target, r.Reason.Clean());
        }
        else
        {
            o.Status = r.Status;
            OpeningRules.AddEvent(db, ctx, clock, o, OpeningEventKind.StatusChanged, from, r.Status, r.Reason.Clean());
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Ok(await OpeningQueries.Detail(db, db.Set<JobOpening>(), o.Uid, clock, ct));
    }

    private static async Task<IResult> Duplicate(Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct)
    {
        if (await db.Set<JobOpening>().AsNoTracking().SingleOrDefaultAsync(o => o.Uid == id, ct) is not { } source)
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        var copy = new JobOpening
        {
            RequestedAt = clock.Today(),
            ClientId = source.ClientId,
            ZoneId = source.ZoneId,
            PositionName = source.PositionName,
            HeadcountRequested = source.HeadcountRequested,
            MonthlySalary = source.MonthlySalary,
            Benefits = [.. source.Benefits],
            Schedule = source.Schedule,
            PaymentBankIds = [.. source.PaymentBankIds],
            EducationLevelId = source.EducationLevelId,
            YearsExperience = source.YearsExperience,
            RequiredExperience = source.RequiredExperience,
            GenderRequirement = source.GenderRequirement,
            AgeRange = source.AgeRange,
            Priority = source.Priority,
            DocumentTemplateId = source.DocumentTemplateId,
            VisibleToClient = source.VisibleToClient,
            Notes = source.Notes,
        };
        db.Add(copy);
        OpeningRules.AddEvent(db, ctx, clock, copy, OpeningEventKind.Created, to: OpeningStatus.Open);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await OpeningQueries.Detail(db, db.Set<JobOpening>(), copy.Uid, clock, ct));
    }

    private static async Task<IResult> AssignRecruiters(Guid id, AssignRecruitersRequest r, DbContext db, RequestContext ctx,
        IClock clock, CancellationToken ct)
    {
        if (await Load(db, id, ct) is not { } o)
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        if (await AssignProblem(db, o, r.UserIds, ct) is { } problem)
            return problem;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var user in r.UserIds.Distinct())
        {
            if (r.Role == AssignmentRole.Lead)
                await OpeningRules.MakeLeadAsync(db, ctx, clock, o, user, ct);
            else
                OpeningRules.Assign(db, ctx, clock, o, user);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Ok(await OpeningQueries.Detail(db, db.Set<JobOpening>(), o.Uid, clock, ct));
    }

    /// <summary>Only active Reclutadores, and only on vacantes that are not cancelled.</summary>
    public static async Task<IResult?> AssignProblem(DbContext db, JobOpening o, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (o.Status == OpeningStatus.Cancelled)
            return ApiResults.Conflict("La vacante está cancelada.");
        var ids = userIds.Distinct().ToList();
        var recruiters = await db.Set<User>().CountAsync(u => ids.Contains(u.Uid) && u.Role == UserRole.Recruiter && u.IsActive, ct);
        return recruiters == ids.Count
            ? null
            : ApiResults.Validation(new Dictionary<string, string[]> { ["userIds"] = [OpeningMessages.NotARecruiter] });
    }

    private static async Task<IResult> Events(Guid id, DbContext db, RequestContext ctx, CancellationToken ct)
    {
        if (!await RecruitingScope.Openings(db, ctx).AnyAsync(o => o.Uid == id, ct))
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        var events = await db.Set<JobOpeningEvent>().Where(e => e.JobOpeningId == id)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).AsNoTracking().ToListAsync(ct);
        var names = await UserNames.Of(db, events.SelectMany(e => new[] { e.UserId, e.ActingForUserId, e.RecruiterUserId }), ct);
        return TypedResults.Ok(events.Select(e => new OpeningEventDto(e.Uid, e.Kind, e.FromStatus, e.ToStatus, e.Reason,
            e.RecruiterUserId, e.RecruiterUserId is { } r ? names.GetValueOrDefault(r) : null,
            UserNames.Author(names, e.UserId, e.ActingForUserId), e.OccurredAt)).ToList());
    }

    private static async Task<IResult> Comments(Guid id, DbContext db, RequestContext ctx, CancellationToken ct)
    {
        if (!await RecruitingScope.Openings(db, ctx).AnyAsync(o => o.Uid == id, ct))
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        var rows = await db.Set<OpeningComment>().Where(c => c.JobOpeningId == id).OrderBy(c => c.CreatedAt).AsNoTracking().ToListAsync(ct);
        var names = await UserNames.Of(db, rows.SelectMany(c => new Guid?[] { c.AuthorUserId, c.ActingForUserId }), ct);
        return TypedResults.Ok(rows.Select(c => new OpeningCommentDto(c.Uid, c.Body,
            UserNames.Author(names, c.AuthorUserId, c.ActingForUserId) ?? "", c.VisibleToClient, c.CreatedAt)).ToList());
    }

    private static async Task<IResult> PostComment(Guid id, OpeningCommentRequest r, DbContext db, RequestContext ctx,
        CancellationToken ct)
    {
        if (!await RecruitingScope.Openings(db, ctx).AnyAsync(o => o.Uid == id, ct))
            return ApiResults.NotFound(RecruitingScope.OpeningNotFound);
        var comment = new OpeningComment
        {
            JobOpeningId = id,
            AuthorUserId = ctx.RequiredPrincipal.UserId,
            ActingForUserId = ctx.ActingFor?.UserId,
            Body = r.Body.Trim(),
            VisibleToClient = r.VisibleToClient,
        };
        db.Add(comment);
        await db.SaveChangesAsync(ct);
        var names = await UserNames.Of(db, [comment.AuthorUserId, comment.ActingForUserId], ct);
        return TypedResults.Ok(new OpeningCommentDto(comment.Uid, comment.Body,
            UserNames.Author(names, comment.AuthorUserId, comment.ActingForUserId) ?? "", comment.VisibleToClient, comment.CreatedAt));
    }
}
