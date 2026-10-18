using FluentValidation;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Candidates;

/// <summary>A row of Candidatos / Mis candidatos and a Kanban card. The vacante is named "Cliente – Puesto" (SPEC D30).</summary>
public sealed record CandidateListItemDto(
    Guid Id,
    Guid PersonId,
    string FullName,
    string? MobilePhone,
    Guid OpeningId,
    string OpeningName,
    string ClientName,
    Guid? RecruiterId,
    string? RecruiterName,
    CandidateSource? Source,
    CandidateStage Stage,
    bool? IsSuitable,
    bool IsHired,
    DateOnly CapturedAt,
    DateOnly? HiredAt,
    int DaysInProcess,
    decimal? OfferedSalary);

public sealed record CandidateDetailDto(
    Guid Id,
    PersonDto Person,
    Guid OpeningId,
    string OpeningName,
    Guid ClientId,
    string ClientName,
    OpeningStatus OpeningStatus,
    Guid? DocumentTemplateId,
    Guid? RecruiterId,
    string? RecruiterName,
    CandidateSource? Source,
    CandidateStage Stage,
    bool? IsSuitable,
    bool IsHired,
    DateOnly CapturedAt,
    DateOnly? HiredAt,
    int DaysInProcess,
    decimal? OfferedSalary,
    string? Comments,
    string? DiscardReason,
    /// <summary>
    /// «Convertir en empleado» (S7): a Contratado not converted yet, for an Administrador with the Personal grant
    /// (<c>payroll_staff</c>); afterwards <see cref="EmployeeId"/> links to the employee.
    /// </summary>
    bool CanConvertToEmployee,
    Guid? EmployeeId);

/// <summary>
/// «Nuevo candidato»: an existing person (<see cref="PersonId"/>) or a new one (<see cref="Person"/>, created in the same
/// transaction with the registry's duplicate check), and an open / in-process vacante.
/// </summary>
public sealed record CreateCandidateRequest(Guid? PersonId, PersonRequest? Person, Guid JobOpeningId, CandidateSource? Source,
    decimal? OfferedSalary, string? Comments, DateOnly? CapturedAt);

public sealed record UpdateCandidateRequest(CandidateSource? Source, decimal? OfferedSalary, string? Comments);

/// <summary>A Kanban move. <see cref="Reason"/> for No apto / Descartado, <see cref="HiredAt"/> (fecha de ingreso) for Contratado.</summary>
public sealed record MoveStageRequest(CandidateStage Stage, string? Reason, DateOnly? HiredAt);

/// <summary>The moved candidate and the vacante's counters, for the toast "HEB – Auxiliar de limpieza: 2 de 3 cubiertas".</summary>
public sealed record MoveStageResultDto(CandidateDetailDto Candidate, string OpeningName, int Hired, int HeadcountRequested,
    OpeningStatus OpeningStatus);

public enum TimelineKind
{
    Stage,
    Note,
    Interview,
}

/// <summary>An entry of the candidate's Historial: stage moves (the first one is the capture), notes added, interviews.</summary>
public sealed record TimelineEntryDto(TimelineKind Kind, CandidateStage? FromStage, CandidateStage? ToStage, string? Text,
    string? Author, DateTimeOffset At);

public sealed record NoteDto(Guid Id, string ContentHtml, string? ContentJson, Guid AuthorUserId, string Author, bool CanEdit,
    DateTimeOffset CreatedAt, DateTimeOffset? EditedAt);

/// <summary>The editor's HTML (sanitized on the server, PLAN D74) and its JSON document for lossless re-editing.</summary>
public sealed record NoteRequest(string ContentHtml, string? ContentJson);

public sealed record InterviewDto(Guid Id, DateTimeOffset ScheduledAt, Guid? InterviewerId, string? InterviewerName, string? Location,
    InterviewOutcome Outcome, string? Notes);

public sealed record InterviewRequest(DateTimeOffset ScheduledAt, Guid? InterviewerId, string? Location, InterviewOutcome Outcome,
    string? Notes);

public static class CandidateMessages
{
    public const string ReasonRequired = "Indica el motivo.";
    public const string HiredAtRequired = "Indica la fecha de ingreso.";
    public const string Converted = "El candidato ya se convirtió en empleado; su contratación no se puede deshacer aquí.";
    public const string SameStage = "El candidato ya está en esa etapa.";
    public const string HiredBeforeCapture = "La fecha de ingreso no puede ser anterior a la fecha de captura.";

    public static string OpeningClosed(OpeningStatus status) => status switch
    {
        OpeningStatus.Paused => "La vacante está pausada; reábrela para mover candidatos.",
        OpeningStatus.Cancelled => "La vacante está cancelada.",
        OpeningStatus.Filled => "La vacante ya está cubierta.",
        _ => "La vacante no admite candidatos.",
    };

    public static string HeadcountReached(int headcount) =>
        (headcount == 1 ? "La vacante ya tiene a su contratado" : $"La vacante ya tiene sus {headcount} contratados")
        + "; aumenta las vacantes solicitadas para contratar a otro.";
}

internal static class MoneyRule
{
    public static bool IsAmount(decimal? m) => m is not { } v || (v >= 0 && v < 10_000_000 && decimal.Round(v, 2) == v);
}

internal sealed class CreateCandidateRequestValidator : AbstractValidator<CreateCandidateRequest>
{
    public CreateCandidateRequestValidator()
    {
        RuleFor(r => r.JobOpeningId).NotEmpty().WithMessage("Elige la vacante.");
        RuleFor(r => r).Must(r => (r.PersonId is null) != (r.Person is null))
            .WithMessage("Elige una persona del registro o captura una nueva.").OverridePropertyName("personId");
        RuleFor(r => r.Person!).SetValidator(new PersonRequestValidator()).When(r => r.Person is not null);
        RuleFor(r => r.Source).NotNull().WithMessage("Elige la fuente.").IsInEnum();
        RuleFor(r => r.OfferedSalary).Must(MoneyRule.IsAmount).WithMessage("El sueldo es un importe en pesos con hasta dos decimales.");
        RuleFor(r => r.Comments).OptionalText(2000);
    }
}

internal sealed class UpdateCandidateRequestValidator : AbstractValidator<UpdateCandidateRequest>
{
    public UpdateCandidateRequestValidator()
    {
        RuleFor(r => r.Source).NotNull().WithMessage("Elige la fuente.").IsInEnum();
        RuleFor(r => r.OfferedSalary).Must(MoneyRule.IsAmount).WithMessage("El sueldo es un importe en pesos con hasta dos decimales.");
        RuleFor(r => r.Comments).OptionalText(2000);
    }
}

internal sealed class MoveStageRequestValidator : AbstractValidator<MoveStageRequest>
{
    public MoveStageRequestValidator()
    {
        RuleFor(r => r.Stage).IsInEnum();
        RuleFor(r => r.Reason).Must(x => x.Clean() is not null)
            .When(r => r.Stage is CandidateStage.NotSuitable or CandidateStage.Discarded).WithMessage(CandidateMessages.ReasonRequired);
        RuleFor(r => r.Reason).OptionalText(500);
        RuleFor(r => r.HiredAt).NotNull().When(r => r.Stage == CandidateStage.Hired).WithMessage(CandidateMessages.HiredAtRequired);
    }
}

internal sealed class NoteRequestValidator : AbstractValidator<NoteRequest>
{
    public NoteRequestValidator()
    {
        RuleFor(r => r.ContentHtml).NotEmpty().WithMessage("Escribe la nota.").MaximumLength(20000)
            .WithMessage("La nota es demasiado larga.");
        RuleFor(r => r.ContentJson).MaximumLength(100_000).WithMessage("La nota es demasiado larga.");
    }
}

internal sealed class InterviewRequestValidator : AbstractValidator<InterviewRequest>
{
    public InterviewRequestValidator()
    {
        RuleFor(r => r.ScheduledAt).NotEmpty().WithMessage("Indica la fecha y hora.");
        RuleFor(r => r.Location).OptionalText(200);
        RuleFor(r => r.Outcome).IsInEnum();
        RuleFor(r => r.Notes).OptionalText(2000);
    }
}
