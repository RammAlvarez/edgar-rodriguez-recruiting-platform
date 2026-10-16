using FluentValidation;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Recruiting.Candidates;

namespace Platform.Recruiting.Openings;

/// <summary>A row of Vacantes / Mis vacantes. <see cref="DisplayName"/> is the only way a vacante is named (SPEC D30).</summary>
public sealed record OpeningListItemDto(
    Guid Id,
    string DisplayName,
    Guid ClientId,
    string ClientName,
    string PositionName,
    Guid? ZoneId,
    string? ZoneName,
    int HeadcountRequested,
    int Hired,
    int Candidates,
    OpeningPriority Priority,
    OpeningStatus Status,
    DateOnly RequestedAt,
    DateOnly? FilledAt,
    int DaysOpen,
    Guid? LeadRecruiterId,
    string? LeadRecruiterName);

public sealed record OpeningRecruiterDto(Guid UserId, string DisplayName, bool IsLead, DateOnly AssignedAt);

public sealed record StageCountDto(CandidateStage Stage, int Count);

public sealed record OpeningDetailDto(
    Guid Id,
    string DisplayName,
    Guid ClientId,
    string ClientName,
    Guid? ZoneId,
    string? ZoneName,
    string PositionName,
    int HeadcountRequested,
    int Hired,
    decimal? MonthlySalary,
    IReadOnlyList<string> Benefits,
    string? Schedule,
    IReadOnlyList<Guid> PaymentBankIds,
    Guid? EducationLevelId,
    int? YearsExperience,
    string? RequiredExperience,
    Gender GenderRequirement,
    string? AgeRange,
    OpeningPriority Priority,
    OpeningStatus Status,
    DateOnly RequestedAt,
    DateOnly? FilledAt,
    int DaysOpen,
    Guid? LeadRecruiterId,
    string? LeadRecruiterName,
    Guid? DocumentTemplateId,
    bool VisibleToClient,
    string? Notes,
    IReadOnlyList<OpeningRecruiterDto> Recruiters,
    IReadOnlyList<StageCountDto> StageCounts);

/// <summary>An option of the «Vacante» dropdown of «Nuevo candidato»: open / in-process vacantes only (SPEC §4.2).</summary>
public sealed record OpeningOptionDto(Guid Id, string DisplayName, int HeadcountRequested, int Hired, string? LeadRecruiterName);

/// <summary>«Nueva vacante» / edit. <see cref="RequestedAt"/> defaults to today; the status is never sent (PLAN D70).</summary>
public sealed record OpeningRequest(
    Guid ClientId,
    Guid? ZoneId,
    string PositionName,
    int HeadcountRequested,
    decimal? MonthlySalary,
    IReadOnlyList<string>? Benefits,
    string? Schedule,
    IReadOnlyList<Guid>? PaymentBankIds,
    Guid? EducationLevelId,
    int? YearsExperience,
    string? RequiredExperience,
    Gender GenderRequirement,
    string? AgeRange,
    OpeningPriority Priority,
    Guid? LeadRecruiterId,
    Guid? DocumentTemplateId,
    bool VisibleToClient,
    string? Notes,
    DateOnly? RequestedAt);

/// <summary>By hand an Administrador pauses, cancels (reason required) or reopens (<c>open</c>, back to the derived status).</summary>
public sealed record OpeningStatusRequest(OpeningStatus Status, string? Reason);

public enum AssignmentRole
{
    Lead,
    Support,
}

/// <summary>«Asignar reclutador» from the vacante, or «Asignar vacante» from the recruiter (several at once).</summary>
public sealed record AssignRecruitersRequest(IReadOnlyList<Guid> UserIds, AssignmentRole Role);

public sealed record AssignOpeningsRequest(IReadOnlyList<Guid> OpeningIds, AssignmentRole Role);

/// <summary>«Quitar»: removing the líder names the new líder among the other assigned recruiters (PLAN D71).</summary>
public sealed record RemoveRecruiterRequest(Guid? NewLeadId);

public sealed record OpeningEventDto(Guid Id, OpeningEventKind Kind, OpeningStatus? FromStatus, OpeningStatus? ToStatus,
    string? Reason, Guid? RecruiterId, string? RecruiterName, string? Author, DateTimeOffset OccurredAt);

public sealed record OpeningCommentDto(Guid Id, string Body, string Author, bool VisibleToClient, DateTimeOffset CreatedAt);

public sealed record OpeningCommentRequest(string Body, bool VisibleToClient);

public static class OpeningMessages
{
    public const string ReasonRequired = "Indica el motivo.";
    public const string NotARecruiter = "Solo un Reclutador activo puede asignarse a una vacante.";
}

internal sealed class OpeningRequestValidator : AbstractValidator<OpeningRequest>
{
    public OpeningRequestValidator()
    {
        RuleFor(r => r.ClientId).NotEmpty().WithMessage("Elige el cliente.");
        RuleFor(r => r.PositionName).NotEmpty().WithMessage("El puesto es obligatorio.").MaximumLength(120);
        RuleFor(r => r.HeadcountRequested).InclusiveBetween(1, 1000).WithMessage("Indica cuántas vacantes (1 o más).");
        RuleFor(r => r.MonthlySalary).Must(m => m is not { } v || (v >= 0 && v < 10_000_000 && decimal.Round(v, 2) == v))
            .WithMessage("El sueldo es un importe en pesos con hasta dos decimales.");
        RuleForEach(r => r.Benefits).NotEmpty().MaximumLength(120).WithMessage("Cada prestación admite hasta 120 caracteres.");
        RuleFor(r => r.Benefits).Must(b => b is null || b.Count <= 30).WithMessage("Hasta 30 prestaciones.");
        RuleFor(r => r.Schedule).OptionalText(200);
        RuleFor(r => r.YearsExperience).InclusiveBetween(0, 60).WithMessage("Años de experiencia entre 0 y 60.");
        RuleFor(r => r.RequiredExperience).OptionalText(1000);
        RuleFor(r => r.GenderRequirement).IsInEnum();
        RuleFor(r => r.AgeRange).OptionalText(40);
        RuleFor(r => r.Priority).IsInEnum();
        RuleFor(r => r.Notes).OptionalText(4000);
    }
}

internal sealed class OpeningStatusRequestValidator : AbstractValidator<OpeningStatusRequest>
{
    public OpeningStatusRequestValidator()
    {
        RuleFor(r => r.Status).Must(s => s is OpeningStatus.Paused or OpeningStatus.Cancelled or OpeningStatus.Open)
            .WithMessage("Solo se puede pausar, cancelar o reabrir una vacante; En proceso y Cubierta se calculan solas.");
        RuleFor(r => r.Reason).Must(r => r.Clean() is not null).When(r => r.Status is OpeningStatus.Paused or OpeningStatus.Cancelled)
            .WithMessage(OpeningMessages.ReasonRequired);
        RuleFor(r => r.Reason).OptionalText(500);
    }
}

internal sealed class AssignRecruitersRequestValidator : AbstractValidator<AssignRecruitersRequest>
{
    public AssignRecruitersRequestValidator()
    {
        RuleFor(r => r.UserIds).NotEmpty().WithMessage("Elige al menos un reclutador.");
        RuleFor(r => r.UserIds).Must(u => u.Count == 1).When(r => r.Role == AssignmentRole.Lead)
            .WithMessage("Una vacante tiene un solo líder.");
        RuleFor(r => r.Role).IsInEnum();
    }
}

internal sealed class AssignOpeningsRequestValidator : AbstractValidator<AssignOpeningsRequest>
{
    public AssignOpeningsRequestValidator()
    {
        RuleFor(r => r.OpeningIds).NotEmpty().WithMessage("Elige al menos una vacante.");
        RuleFor(r => r.Role).IsInEnum();
    }
}

internal sealed class RemoveRecruiterRequestValidator : AbstractValidator<RemoveRecruiterRequest>;

internal sealed class OpeningCommentRequestValidator : AbstractValidator<OpeningCommentRequest>
{
    public OpeningCommentRequestValidator() =>
        RuleFor(r => r.Body).Must(b => b.Clean() is not null).WithMessage("Escribe el comentario.").MaximumLength(2000);
}
