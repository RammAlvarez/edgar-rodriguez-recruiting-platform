using FluentValidation;
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
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Portal;

/// <summary>
/// A card of «Mis vacantes» (SPEC §4.6). Its own shape, so the vacante's sueldo, Sexo and Edad (D22) can never reach a client.
/// </summary>
public sealed record ClientOpeningDto(
    Guid Id,
    string DisplayName,
    string PositionName,
    string? ZoneName,
    OpeningStatus Status,
    int HeadcountRequested,
    int Hired,
    DateOnly RequestedAt,
    DateOnly? FilledAt,
    int DaysOpen,
    string? LeadRecruiterName,
    string? LeadRecruiterPhone,
    string? LeadRecruiterEmail,
    IReadOnlyList<StageCountDto> StageCounts);

/// <summary>
/// A candidate as the client sees them (SPEC §4.6, DATA_MODEL §8): before Apto only initials, fuente, etapa and fecha de
/// captura (<see cref="IsIdentified"/> false, the other fields null); at Apto or Contratado the full name, sueldo ofrecido
/// (D20), fecha de entrevista and documents completeness — never the documents.
/// </summary>
public sealed record ClientCandidateDto(
    Guid Id,
    bool IsIdentified,
    string Name,
    CandidateSource? Source,
    CandidateStage Stage,
    DateOnly CapturedAt,
    decimal? OfferedSalary,
    DateTimeOffset? InterviewAt,
    decimal? DocumentsCompleteness);

public sealed record ClientOpeningDetailDto(ClientOpeningDto Opening, IReadOnlyList<ClientCandidateDto> Candidates);

/// <summary>A message of a vacante's thread; <see cref="FromClient"/> tells the client's own side from the recruiters'.</summary>
public sealed record ClientCommentDto(Guid Id, Guid OpeningId, string OpeningName, string Body, string Author, bool FromClient,
    DateTimeOffset CreatedAt);

public sealed record ClientCommentRequest(string Body);

internal sealed class ClientCommentRequestValidator : AbstractValidator<ClientCommentRequest>
{
    public ClientCommentRequestValidator() =>
        RuleFor(r => r.Body).Must(b => b.Clean() is not null).WithMessage("Escribe el comentario.").MaximumLength(2000);
}

/// <summary>
/// The Clients app (SPEC §4.6, DATA_MODEL §8 row <c>client</c>, PLAN D78): read-only progress of the client's own vacantes
/// marked visible, and a comment thread per vacante. Every query is limited to the caller's <c>client_id</c>.
/// </summary>
public static class ClientPortalEndpoints
{
    public const string Tag = "client-portal";

    public const string NotFound = "La vacante no existe o no está disponible en el portal.";

    private static readonly AccessRule Client = AccessRule.ForRoles(UserRole.Client);

    /// <summary>The stages from which the client sees who the candidate is (DATA_MODEL §8).</summary>
    public static readonly CandidateStage[] Identified = [CandidateStage.Suitable, CandidateStage.Hired];

    public static IEndpointRouteBuilder MapClientPortalEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/client-portal").WithTags("Client portal");

        g.MapGet("/openings", async (DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                TypedResults.Ok(await Cards(db, Openings(db, ctx), clock, ct)))
            .WithName("ListClientOpenings").RequireAccess(Client);

        g.MapGet("/openings/{id:guid}", Detail).WithName("GetClientOpening").Produces<ClientOpeningDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Client);

        g.MapGet("/openings/{id:guid}/comments", async (Guid id, DbContext db, RequestContext ctx, CancellationToken ct) =>
                await Openings(db, ctx).AnyAsync(o => o.Uid == id, ct)
                    ? TypedResults.Ok(await Comments(db, db.Set<OpeningComment>().Where(c => c.JobOpeningId == id), false, ct))
                    : ApiResults.NotFound(NotFound))
            .WithName("ListClientOpeningComments").Produces<List<ClientCommentDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(Client);

        g.MapPost("/openings/{id:guid}/comments", async (Guid id, ClientCommentRequest r, DbContext db, RequestContext ctx,
                CancellationToken ct) =>
            {
                if (!await Openings(db, ctx).AnyAsync(o => o.Uid == id, ct))
                    return ApiResults.NotFound(NotFound);
                // The client's side of the thread is always visible to both sides (D75, D78).
                var comment = new OpeningComment
                {
                    JobOpeningId = id,
                    AuthorUserId = ctx.RequiredPrincipal.UserId,
                    Body = r.Body.Trim(),
                    VisibleToClient = true,
                };
                db.Add(comment);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok((await Comments(db, db.Set<OpeningComment>().Where(c => c.Uid == comment.Uid), false, ct)).Single());
            })
            .WithName("PostClientOpeningComment").Validate<ClientCommentRequest>().Produces<ClientCommentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Client).Invalidates(Tag, OpeningEndpoints.Tag);

        // «Comentarios»: the latest messages across the client's vacantes, newest first.
        g.MapGet("/comments", async (DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var openings = Openings(db, ctx).Select(o => o.Uid);
                return TypedResults.Ok(await Comments(db, db.Set<OpeningComment>().Where(c => openings.Contains(c.JobOpeningId)), true, ct));
            })
            .WithName("ListClientComments").RequireAccess(Client);

        return v1;
    }

    /// <summary>DATA_MODEL §8: <c>client_id = :client_id AND visible_to_client</c>.</summary>
    public static IQueryable<JobOpening> Openings(DbContext db, RequestContext ctx)
    {
        var client = ctx.RequiredPrincipal.ClientId;
        return db.Set<JobOpening>().Where(o => o.ClientId == client && o.VisibleToClient);
    }

    private static async Task<List<ClientOpeningDto>> Cards(DbContext db, IQueryable<JobOpening> q, IClock clock, CancellationToken ct)
    {
        var rows = await q.AsNoTracking()
            .Select(o => new
            {
                o,
                Zone = db.Set<Zone>().Where(z => z.Uid == o.ZoneId).Select(z => z.NameEs).FirstOrDefault(),
                Lead = db.Set<User>().Where(u => u.Uid == o.LeadRecruiterId).Select(u => new { u.DisplayName, u.Phone, u.Email })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.o.Uid).ToList();
        var names = await OpeningQueries.DisplayNames(db, ids, ct);
        var stages = (await db.Set<Candidate>().Where(c => ids.Contains(c.JobOpeningId))
                .GroupBy(c => new { c.JobOpeningId, c.Stage }).Select(g => new { g.Key.JobOpeningId, g.Key.Stage, Count = g.Count() })
                .ToListAsync(ct))
            .ToLookup(x => x.JobOpeningId);
        var today = clock.Today();
        return rows
            // Open and in-process first, then the rest; newest request first within each.
            .OrderBy(r => OpeningRules.Active.Contains(r.o.Status) ? 0 : 1).ThenByDescending(r => r.o.RequestedAt).ThenByDescending(r => r.o.Id)
            .Select(r =>
            {
                var counts = stages[r.o.Uid].ToDictionary(x => x.Stage, x => x.Count);
                return new ClientOpeningDto(r.o.Uid, names[r.o.Uid], r.o.PositionName, r.Zone, r.o.Status, r.o.HeadcountRequested,
                    counts.GetValueOrDefault(CandidateStage.Hired), r.o.RequestedAt, r.o.FilledAt, r.o.DaysOpen(today),
                    r.Lead?.DisplayName, r.Lead?.Phone, r.Lead?.Email,
                    Enum.GetValues<CandidateStage>().Select(s => new StageCountDto(s, counts.GetValueOrDefault(s))).ToList());
            })
            .ToList();
    }

    private static async Task<IResult> Detail(Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct)
    {
        var card = (await Cards(db, Openings(db, ctx).Where(o => o.Uid == id), clock, ct)).SingleOrDefault();
        if (card is null)
            return ApiResults.NotFound(NotFound);
        var templateId = await db.Set<JobOpening>().Where(o => o.Uid == id).Select(o => o.DocumentTemplateId).SingleAsync(ct)
            ?? await db.Set<DocumentTemplate>().Where(t => t.Scope == DocumentTemplateScope.Candidate && t.IsDefault)
                .Select(t => (Guid?)t.Uid).SingleOrDefaultAsync(ct);
        var rows = await db.Set<Candidate>().AsNoTracking().Where(c => c.JobOpeningId == id)
            .OrderByDescending(c => c.CapturedAt).ThenByDescending(c => c.Id)
            .Join(db.Set<Person>(), c => c.PersonId, p => p.Uid, (c, p) => new { c, p.FirstNames, p.LastNamePaternal, p.FullName })
            .ToListAsync(ct);
        var identifiedIds = rows.Where(r => Identified.Contains(r.c.Stage)).Select(r => r.c.Uid).ToList();
        var now = clock.UtcNow;
        var interviews = (await db.Set<Interview>().AsNoTracking().Where(i => identifiedIds.Contains(i.CandidateId)).ToListAsync(ct))
            .ToLookup(i => i.CandidateId);

        var candidates = new List<ClientCandidateDto>();
        foreach (var r in rows)
        {
            if (!Identified.Contains(r.c.Stage))
            {
                candidates.Add(new ClientCandidateDto(r.c.Uid, false, Initials(r.FirstNames, r.LastNamePaternal), r.c.Source, r.c.Stage,
                    r.c.CapturedAt, null, null, null));
                continue;
            }
            // The next pending interview, otherwise the latest one held.
            var mine = interviews[r.c.Uid].ToList();
            var interview = mine.Where(i => i.ScheduledAt >= now && i.Outcome == InterviewOutcome.Pending).MinBy(i => i.ScheduledAt)
                ?? mine.MaxBy(i => i.ScheduledAt);
            var completeness = templateId is { } t ? await DocumentEndpoints.Completeness(db, r.c.PersonId, t, ct) : (decimal?)null;
            candidates.Add(new ClientCandidateDto(r.c.Uid, true, r.FullName, r.c.Source, r.c.Stage, r.c.CapturedAt, r.c.OfferedSalary,
                interview?.ScheduledAt, completeness));
        }
        return TypedResults.Ok(new ClientOpeningDetailDto(card, candidates));
    }

    /// <summary>"J. R.": first name and paternal surname initials (prototype).</summary>
    public static string Initials(string firstNames, string lastNamePaternal) =>
        string.Join(' ', new[] { firstNames, lastNamePaternal }.Where(s => s.Trim().Length > 0).Select(s => $"{char.ToUpperInvariant(s.Trim()[0])}."));

    private static async Task<List<ClientCommentDto>> Comments(DbContext db, IQueryable<OpeningComment> q, bool newestFirst,
        CancellationToken ct)
    {
        var visible = q.Where(c => c.VisibleToClient);
        var rows = await (newestFirst ? visible.OrderByDescending(c => c.CreatedAt) : visible.OrderBy(c => c.CreatedAt))
            .Take(200).AsNoTracking().ToListAsync(ct);
        var userIds = rows.SelectMany(c => new Guid?[] { c.AuthorUserId, c.ActingForUserId }).OfType<Guid>().Distinct().ToList();
        var users = await db.Set<User>().AsNoTracking().Where(u => userIds.Contains(u.Uid))
            .ToDictionaryAsync(u => u.Uid, u => new { u.DisplayName, u.Role }, ct);
        var names = users.ToDictionary(u => u.Key, u => u.Value.DisplayName);
        var openings = await OpeningQueries.DisplayNames(db, rows.Select(c => c.JobOpeningId).Distinct().ToList(), ct);
        return rows.Select(c => new ClientCommentDto(c.Uid, c.JobOpeningId, openings[c.JobOpeningId], c.Body,
            UserNames.Author(names, c.AuthorUserId, c.ActingForUserId) ?? "", users.GetValueOrDefault(c.AuthorUserId)?.Role == UserRole.Client,
            c.CreatedAt)).ToList();
    }
}
