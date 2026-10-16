using System.Text;
using FluentValidation;

namespace Platform.Core.People;

public sealed record PresignUploadRequest(Guid PersonId, Guid DocumentTypeId, string FileName, string ContentType, long SizeBytes);

/// <summary>The browser PUTs the file to <see cref="Url"/> with <see cref="Headers"/>, then confirms with <see cref="StorageKey"/>.</summary>
public sealed record PresignUploadResponse(string StorageKey, string Url, IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset ExpiresAt);

public sealed record ConfirmDocumentRequest(Guid DocumentTypeId, string StorageKey, string OriginalFilename, DateOnly? IssuedAt,
    DateOnly? ExpiresAt);

public sealed record RejectDocumentRequest(string Reason);

public sealed record PersonDocumentDto(
    Guid Id,
    Guid DocumentTypeId,
    DocumentStatus Status,
    string OriginalFilename,
    string ContentType,
    long SizeBytes,
    DateOnly? IssuedAt,
    DateOnly? ExpiresAt,
    Guid? ValidatedBy,
    DateTimeOffset? ValidatedAt,
    string? RejectionReason,
    int Version,
    DateTimeOffset UploadedAt)
{
    public static PersonDocumentDto From(PersonDocument d) => new(d.Uid, d.DocumentTypeId, d.Status, d.OriginalFilename,
        d.ContentType, d.SizeBytes, d.IssuedAt, d.ExpiresAt, d.ValidatedBy, d.ValidatedAt, d.RejectionReason, d.Version, d.CreatedAt);
}

/// <summary>One line of the checklist: a template item with its current document, or a document outside the template.</summary>
public sealed record ChecklistItemDto(Guid DocumentTypeId, string Code, string NameEs, bool HasExpiry, bool InTemplate,
    bool IsRequired, PersonDocumentDto? Current);

public sealed record DocumentChecklistDto(Guid PersonId, Guid TemplateId, string TemplateName, decimal Completeness,
    IReadOnlyList<ChecklistItemDto> Items);

public sealed record DocumentUrlDto(string Url, DateTimeOffset ExpiresAt);

public static class DocumentFiles
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Accepted types (SPEC §3.2: PDF/JPG/PNG) and the extension the API gives the object key.</summary>
    public static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = "pdf",
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
    };

    /// <summary><c>people/&lt;person uid&gt;/&lt;CODE&gt;/</c> (DATA_MODEL §3.10); the object name is a new uuid, never the user's file name.</summary>
    public static string KeyPrefix(Guid personId, string documentTypeCode) => $"people/{personId}/{documentTypeCode}/";

    /// <summary>
    /// The user's file name as shown and offered on download: no path, no control or reserved characters, at most 120
    /// characters, the extension of the actual type.
    /// </summary>
    public static string SanitizeFileName(string? name, string contentType)
    {
        var bare = (name ?? "").Replace('\\', '/').Split('/').Last().Normalize(NormalizationForm.FormC);
        var sb = new StringBuilder();
        foreach (var c in bare)
            sb.Append(char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' or '(' or ')' ? c : '_');
        var cleaned = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ', '_');
        var ext = Extensions.GetValueOrDefault(contentType, "bin");
        var stem = cleaned.Contains('.') ? cleaned[..cleaned.LastIndexOf('.')].TrimEnd('.', ' ') : cleaned;
        if (stem.Length == 0)
            stem = "documento";
        if (stem.Length > 115)
            stem = stem[..115];
        return $"{stem}.{ext}";
    }
}

internal sealed class PresignUploadRequestValidator : AbstractValidator<PresignUploadRequest>
{
    public PresignUploadRequestValidator()
    {
        RuleFor(r => r.ContentType).Must(DocumentFiles.Extensions.ContainsKey)
            .WithMessage("Sube un PDF, JPG o PNG.");
        RuleFor(r => r.SizeBytes).InclusiveBetween(1, DocumentFiles.MaxBytes).WithMessage("El archivo debe pesar como máximo 10 MB.");
        RuleFor(r => r.FileName).NotEmpty().WithMessage("Falta el nombre del archivo.").MaximumLength(255);
    }
}

internal sealed class ConfirmDocumentRequestValidator : AbstractValidator<ConfirmDocumentRequest>
{
    public ConfirmDocumentRequestValidator()
    {
        RuleFor(r => r.StorageKey).NotEmpty().MaximumLength(300);
        RuleFor(r => r.OriginalFilename).NotEmpty().WithMessage("Falta el nombre del archivo.").MaximumLength(255);
        RuleFor(r => r.ExpiresAt).GreaterThanOrEqualTo(r => r.IssuedAt).When(r => r.IssuedAt is not null && r.ExpiresAt is not null)
            .WithMessage("La fecha de vencimiento es anterior a la de expedición.");
    }
}

internal sealed class RejectDocumentRequestValidator : AbstractValidator<RejectDocumentRequest>
{
    public RejectDocumentRequestValidator() =>
        RuleFor(r => r.Reason).NotEmpty().WithMessage("Indica el motivo del rechazo; la persona lo verá.").MaximumLength(300);
}
