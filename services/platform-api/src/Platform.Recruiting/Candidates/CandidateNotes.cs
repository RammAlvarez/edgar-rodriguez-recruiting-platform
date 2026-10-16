using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Http;

namespace Platform.Recruiting.Candidates;

/// <summary>«Notas» of the candidate detail (SPEC §4.2, PLAN D74): newest first, edited by the author or an Administrador.</summary>
internal static class CandidateNotes
{
    public const string NotFound = "La nota no existe.";
    public const string Blank = "Escribe la nota.";

    public static void Map(RouteGroupBuilder g)
    {
        g.MapGet("/{id:guid}/notes", async (Guid id, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == id, ct))
                    return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
                var notes = await db.Set<CandidateNote>().Where(n => n.CandidateId == id && n.DeletedAt == null)
                    .OrderByDescending(n => n.CreatedAt).AsNoTracking().ToListAsync(ct);
                var names = await UserNames.Of(db, notes.SelectMany(n => new Guid?[] { n.AuthorUserId, n.ActingForUserId }), ct);
                return TypedResults.Ok(notes.Select(n => Dto(n, names, ctx)).ToList());
            })
            .WithName("ListCandidateNotes").Produces<List<NoteDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(RecruitingScope.Staff);

        g.MapPost("/{id:guid}/notes", async (Guid id, NoteRequest r, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == id, ct))
                    return ApiResults.NotFound(RecruitingScope.CandidateNotFound);
                var html = NoteHtml.Sanitize(r.ContentHtml);
                if (NoteHtml.IsBlank(html))
                    return BlankProblem();
                var note = new CandidateNote
                {
                    CandidateId = id,
                    AuthorUserId = ctx.RequiredPrincipal.UserId,
                    ActingForUserId = ctx.ActingFor?.UserId,
                    ContentHtml = html,
                    ContentJson = r.ContentJson,
                };
                db.Add(note);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(Dto(note, await UserNames.Of(db, [note.AuthorUserId, note.ActingForUserId], ct), ctx));
            })
            .WithName("CreateCandidateNote").Validate<NoteRequest>().Produces<NoteDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(CandidateEndpoints.Tag).RequireAccess(RecruitingScope.Staff);

        g.MapPut("/{id:guid}/notes/{noteId:guid}", async (Guid id, Guid noteId, NoteRequest r, DbContext db, RequestContext ctx,
                IClock clock, CancellationToken ct) =>
            {
                if (await Editable(db, ctx, id, noteId, ct) is not { } note)
                    return ApiResults.NotFound(NotFound);
                var html = NoteHtml.Sanitize(r.ContentHtml);
                if (NoteHtml.IsBlank(html))
                    return BlankProblem();
                note.ContentHtml = html;
                note.ContentJson = r.ContentJson;
                note.EditedAt = clock.UtcNow;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(Dto(note, await UserNames.Of(db, [note.AuthorUserId, note.ActingForUserId], ct), ctx));
            })
            .WithName("UpdateCandidateNote").Validate<NoteRequest>().Produces<NoteDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(CandidateEndpoints.Tag).RequireAccess(RecruitingScope.Staff);

        g.MapDelete("/{id:guid}/notes/{noteId:guid}", async (Guid id, Guid noteId, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                if (await Editable(db, ctx, id, noteId, ct) is not { } note)
                    return ApiResults.NotFound(NotFound);
                note.DeletedAt = clock.UtcNow;
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteCandidateNote").ProducesProblem(StatusCodes.Status404NotFound).Invalidates(CandidateEndpoints.Tag)
            .RequireAccess(RecruitingScope.Staff);
    }

    private static IResult BlankProblem() =>
        ApiResults.Validation(new Dictionary<string, string[]> { ["contentHtml"] = [Blank] });

    /// <summary>Someone else's note answers 404 for a Reclutador, so another author's notes cannot be probed.</summary>
    private static async Task<CandidateNote?> Editable(DbContext db, RequestContext ctx, Guid candidateId, Guid noteId, CancellationToken ct)
    {
        if (!await RecruitingScope.Candidates(db, ctx).AnyAsync(c => c.Uid == candidateId, ct))
            return null;
        var note = await db.Set<CandidateNote>()
            .SingleOrDefaultAsync(n => n.Uid == noteId && n.CandidateId == candidateId && n.DeletedAt == null, ct);
        return note is not null && CanEdit(note, ctx) ? note : null;
    }

    private static bool CanEdit(CandidateNote n, RequestContext ctx) =>
        ctx.RequiredPrincipal.IsAdministrator || n.AuthorUserId == ctx.RequiredPrincipal.UserId;

    private static NoteDto Dto(CandidateNote n, IReadOnlyDictionary<Guid, string> names, RequestContext ctx) =>
        new(n.Uid, n.ContentHtml, n.ContentJson, n.AuthorUserId, UserNames.Author(names, n.AuthorUserId, n.ActingForUserId) ?? "",
            CanEdit(n, ctx), n.CreatedAt, n.EditedAt);
}
