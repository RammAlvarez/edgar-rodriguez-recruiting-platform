using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Catalogs;
using Platform.Core.Http;
using Platform.Core.Users;

namespace Platform.Core.People;

/// <summary>
/// The document vault (SPEC §3.2, ARCHITECTURE §4 "Files"): presigned PUT with a key decided here → the browser uploads →
/// confirm records the metadata after checking the object; downloads are presigned GETs of 15 minutes, logged in
/// <c>data_access_log</c>. Administradores and Reclutadores, each person checked through <see cref="IPersonScope"/> (PLAN D73);
/// a candidate reads, uploads and views only their own documents and never validates or rejects (PLAN D77).
/// </summary>
public static class DocumentEndpoints
{
    public const string Tag = "documents";

    public const string NotFound = "El documento no existe.";

    private static readonly AccessRule Staff = AccessRule.ForRoles(UserRole.Administrator, UserRole.Recruiter);

    /// <summary>Staff, or the candidate on their own person (checked by <see cref="IPersonScope"/>).</summary>
    private static readonly AccessRule StaffOrCandidate = Staff with { AllowsCandidate = true };

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/files/presign-upload", async (PresignUploadRequest r, DbContext db, IObjectStorage storage, IPersonScope scope,
                CancellationToken ct) =>
            {
                if (!await scope.CanAccessAsync(r.PersonId, ct) || !await db.Set<Person>().AnyAsync(p => p.Uid == r.PersonId && p.DeletedAt == null, ct))
                    return ApiResults.NotFound(PeopleEndpoints.NotFound);
                if (await db.Set<DocumentType>().SingleOrDefaultAsync(t => t.Uid == r.DocumentTypeId && t.IsActive, ct) is not { } type)
                    return ApiResults.NotFound("El tipo de documento no existe.");
                var key = $"{DocumentFiles.KeyPrefix(r.PersonId, type.Code)}{Guid.CreateVersion7()}.{DocumentFiles.Extensions[r.ContentType]}";
                var put = await storage.PresignUploadAsync(StorageBucket.Documents, key, r.ContentType.ToLowerInvariant(),
                    r.SizeBytes, DocumentFiles.UrlLifetime, ct);
                return TypedResults.Ok(new PresignUploadResponse(key, put.Url, put.Headers, put.ExpiresAt));
            })
            .WithName("PresignDocumentUpload").WithTags("Documents").Validate<PresignUploadRequest>()
            .Produces<PresignUploadResponse>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(StaffOrCandidate).Invalidates(Tag).RequireRateLimiting(RateLimitPolicies.Uploads);

        var people = v1.MapGroup("/people/{personId:guid}/documents").WithTags("Documents");

        people.MapGet("", async (Guid personId, Guid? templateId, DbContext db, IPersonScope scope, CancellationToken ct) =>
                await scope.CanAccessAsync(personId, ct) && await Checklist(db, personId, templateId, ct) is { } list
                    ? TypedResults.Ok(list)
                    : ApiResults.NotFound(PeopleEndpoints.NotFound))
            .WithName("GetDocumentChecklist").Produces<DocumentChecklistDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(StaffOrCandidate);

        people.MapPost("", Confirm)
            .WithName("ConfirmDocumentUpload").Validate<ConfirmDocumentRequest>().Produces<PersonDocumentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(StaffOrCandidate).Invalidates(Tag, PeopleEndpoints.Tag);

        var docs = v1.MapGroup("/documents/{id:guid}").WithTags("Documents");

        docs.MapPost("/validate", (Guid id, DbContext db, RequestContext ctx, IClock clock, IPersonScope scope, CancellationToken ct) =>
                Review(db, scope, id, d =>
                {
                    d.Status = DocumentStatus.Validated;
                    d.ValidatedBy = ctx.RequiredPrincipal.UserId;
                    d.ValidatedAt = clock.UtcNow;
                    d.RejectionReason = null;
                }, ct))
            .WithName("ValidateDocument").Produces<PersonDocumentDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).RequireAccess(Staff).Invalidates(Tag, PeopleEndpoints.Tag);

        docs.MapPost("/reject", (Guid id, RejectDocumentRequest r, DbContext db, RequestContext ctx, IClock clock, IPersonScope scope,
                CancellationToken ct) =>
                Review(db, scope, id, d =>
                {
                    d.Status = DocumentStatus.Rejected;
                    d.RejectionReason = r.Reason.Trim();
                    d.ValidatedBy = ctx.RequiredPrincipal.UserId;
                    d.ValidatedAt = clock.UtcNow;
                }, ct))
            .WithName("RejectDocument").Validate<RejectDocumentRequest>().Produces<PersonDocumentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAccess(Staff).Invalidates(Tag, PeopleEndpoints.Tag);

        // A GET that writes the read log: viewing (inline) or downloading (attachment) personal data is what is logged.
        docs.MapGet("/url", async (Guid id, bool? download, DbContext db, IObjectStorage storage, DataAccess log, IPersonScope scope,
                CancellationToken ct) =>
            {
                if (await db.Set<PersonDocument>().AsNoTracking().SingleOrDefaultAsync(d => d.Uid == id, ct) is not { } doc
                    || !await scope.CanAccessAsync(doc.PersonId, ct))
                    return ApiResults.NotFound(NotFound);
                var attachment = download == true;
                var url = await storage.PresignDownloadAsync(StorageBucket.Documents, doc.StorageKey,
                    attachment ? doc.OriginalFilename : null, DocumentFiles.UrlLifetime, ct);
                await log.RecordAsync(doc.PersonId, attachment ? DataAccessKind.DownloadDocument : DataAccessKind.ViewDocument, doc.Uid, ct);
                return TypedResults.Ok(new DocumentUrlDto(url.Url, url.ExpiresAt));
            })
            .WithName("GetDocumentUrl").Produces<DocumentUrlDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(StaffOrCandidate);

        return v1;
    }

    private static async Task<IResult> Confirm(Guid personId, ConfirmDocumentRequest r, DbContext db, IObjectStorage storage,
        IClock clock, IPersonScope scope, RequestContext ctx, IEnumerable<Notifications.ICandidateDocumentListener> listeners, CancellationToken ct)
    {
        if (!await scope.CanAccessAsync(personId, ct) || !await db.Set<Person>().AnyAsync(p => p.Uid == personId && p.DeletedAt == null, ct))
            return ApiResults.NotFound(PeopleEndpoints.NotFound);
        if (await db.Set<DocumentType>().SingleOrDefaultAsync(t => t.Uid == r.DocumentTypeId, ct) is not { } type)
            return ApiResults.NotFound("El tipo de documento no existe.");

        static IResult Field(string field, string message) =>
            ApiResults.Validation(new Dictionary<string, string[]> { [field] = [message] });

        // Only keys this API handed out for this person and type are accepted.
        var prefix = DocumentFiles.KeyPrefix(personId, type.Code);
        if (!r.StorageKey.StartsWith(prefix, StringComparison.Ordinal) || r.StorageKey.AsSpan(prefix.Length).ContainsAny("/\\"))
            return Field("storageKey", "El archivo no corresponde a esta persona o a este documento.");
        // One confirm per uploaded key at a time (a double-click, a retry): the second waits for the first to move the file and
        // commit, then finds nothing left to register instead of failing mid-copy or registering the same upload twice.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({r.StorageKey}))", ct);
        if (await db.Set<PersonDocument>().AnyAsync(d => d.StorageKey == r.StorageKey, ct))
            return Field("storageKey", "Este archivo ya se registró.");
        var today = clock.Today();
        var expiresAt = type.HasExpiry
            ? r.ExpiresAt ?? (type.DefaultValidityDays is { } days ? (r.IssuedAt ?? today).AddDays(days) : null)
            : null;
        if (expiresAt is { } e && e < today)
            return Field("expiresAt", "El documento ya está vencido; sube uno vigente.");

        if (await storage.HeadAsync(StorageBucket.Documents, r.StorageKey, ct) is not { } uploaded)
            return Field("storageKey", "No encontramos el archivo; vuelve a subirlo.");
        if (uploaded.SizeBytes is <= 0 or > DocumentFiles.MaxBytes)
        {
            await storage.DeleteAsync(StorageBucket.Documents, r.StorageKey, ct);
            return Field("storageKey", "Sube un PDF, JPG o PNG de máximo 10 MB.");
        }

        // The upload URL stays valid after this point, so the uploaded key could be overwritten with anything. Copy it
        // to a key nobody holds a URL for, check the copy, and keep only the copy (it is what staff later download).
        var contentType = (uploaded.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        if (!DocumentFiles.Extensions.TryGetValue(contentType, out var extension))
        {
            await storage.DeleteAsync(StorageBucket.Documents, r.StorageKey, ct);
            return Field("storageKey", "Sube un PDF, JPG o PNG de máximo 10 MB.");
        }
        var finalKey = $"{prefix}stored/{Guid.CreateVersion7()}.{extension}";
        await storage.CopyAsync(StorageBucket.Documents, r.StorageKey, finalKey, ct);
        await storage.DeleteAsync(StorageBucket.Documents, r.StorageKey, ct);
        var stored = await storage.HeadAsync(StorageBucket.Documents, finalKey, ct);
        contentType = (stored?.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        if (stored is null || stored.SizeBytes is <= 0 or > DocumentFiles.MaxBytes || !DocumentFiles.Extensions.ContainsKey(contentType))
        {
            await storage.DeleteAsync(StorageBucket.Documents, finalKey, ct);
            return Field("storageKey", "Sube un PDF, JPG o PNG de máximo 10 MB.");
        }
        if (!DocumentFiles.HasSignature(contentType, await storage.ReadPrefixAsync(StorageBucket.Documents, finalKey, 8, ct)))
        {
            await storage.DeleteAsync(StorageBucket.Documents, finalKey, ct);
            return Field("storageKey", DocumentFiles.NotAFile);
        }

        var previous = await db.Set<PersonDocument>()
            .SingleOrDefaultAsync(d => d.PersonId == personId && d.DocumentTypeId == type.Uid && d.SupersededBy == null, ct);
        var doc = new PersonDocument
        {
            PersonId = personId,
            DocumentTypeId = type.Uid,
            StorageKey = finalKey,
            OriginalFilename = DocumentFiles.SanitizeFileName(r.OriginalFilename, contentType),
            ContentType = contentType,
            SizeBytes = stored.SizeBytes,
            IssuedAt = r.IssuedAt,
            ExpiresAt = expiresAt,
            Version = (previous?.Version ?? 0) + 1,
        };
        if (previous is not null)
        {
            // Free the "current" slot first (partial unique index); the superseded_by FK is deferred to the commit.
            previous.SupersededBy = doc.Uid;
            await db.SaveChangesAsync(ct);
        }
        db.Add(doc);
        await db.SaveChangesAsync(ct);
        // A candidate's own upload tells their recruiters (SPEC §3.5).
        if (ctx.Candidate is not null)
        {
            foreach (var listener in listeners)
                await listener.UploadedAsync(db, personId, doc.Uid, type.NameEs, clock.UtcNow, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return TypedResults.Ok(PersonDocumentDto.From(doc));
    }

    private static async Task<IResult> Review(DbContext db, IPersonScope scope, Guid id, Action<PersonDocument> apply, CancellationToken ct)
    {
        if (await db.Set<PersonDocument>().SingleOrDefaultAsync(d => d.Uid == id, ct) is not { } doc
            || !await scope.CanAccessAsync(doc.PersonId, ct))
            return ApiResults.NotFound(NotFound);
        if (doc.SupersededBy is not null)
            return ApiResults.Conflict("Hay una versión más reciente de este documento; revisa esa.");
        apply(doc);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(PersonDocumentDto.From(doc));
    }

    public static async Task<DocumentChecklistDto?> Checklist(DbContext db, Guid personId, Guid? templateId, CancellationToken ct)
    {
        if (!await db.Set<Person>().AnyAsync(p => p.Uid == personId && p.DeletedAt == null, ct))
            return null;
        var template = await db.Set<DocumentTemplate>().AsNoTracking().Include(t => t.Items)
            .Where(t => templateId == null ? t.Scope == DocumentTemplateScope.Candidate && t.IsDefault : t.Uid == templateId)
            .SingleOrDefaultAsync(ct);
        if (template is null)
            return null;
        var types = await db.Set<DocumentType>().AsNoTracking().ToDictionaryAsync(t => t.Uid, ct);
        var current = await db.Set<PersonDocument>().AsNoTracking()
            .Where(d => d.PersonId == personId && d.SupersededBy == null).ToDictionaryAsync(d => d.DocumentTypeId, ct);
        var completeness = await Completeness(db, personId, template.Uid, ct);

        ChecklistItemDto Item(Guid typeId, bool inTemplate, bool required)
        {
            var t = types[typeId];
            return new ChecklistItemDto(typeId, t.Code, t.NameEs, t.HasExpiry, inTemplate, required,
                current.TryGetValue(typeId, out var d) ? PersonDocumentDto.From(d) : null);
        }

        var inTemplate = template.Items.OrderBy(i => types[i.DocumentTypeId].SortOrder)
            .Select(i => Item(i.DocumentTypeId, true, i.IsRequired));
        var extra = current.Keys.Where(k => template.Items.All(i => i.DocumentTypeId != k)).OrderBy(k => types[k].SortOrder)
            .Select(k => Item(k, false, false));
        return new DocumentChecklistDto(personId, template.Uid, template.NameEs, completeness, [.. inTemplate, .. extra]);
    }

    /// <summary>From the view <c>person_document_completeness</c> (created by SQL in the migration, read by both apps).</summary>
    public static Task<decimal> Completeness(DbContext db, Guid personId, Guid templateId, CancellationToken ct) =>
        db.Database.SqlQuery<decimal>($"""
            SELECT completeness AS "Value" FROM platform.person_document_completeness
            WHERE person_id = {personId} AND template_id = {templateId}
            """).SingleAsync(ct);
}
