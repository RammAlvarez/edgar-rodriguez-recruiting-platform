using Platform.Core.Abstractions;
using Platform.Core.People;

namespace Platform.Recruiting.Openings;

/// <summary>Postgres enum <c>opening_status</c>. <see cref="InProgress"/> and <see cref="Filled"/> are derived (PLAN D70).</summary>
public enum OpeningStatus
{
    Open,
    InProgress,
    Filled,
    Cancelled,
    Paused,
}

/// <summary>Postgres enum <c>opening_priority</c>.</summary>
public enum OpeningPriority
{
    High,
    Medium,
    Low,
}

/// <summary>Postgres enum <c>opening_event_kind</c>: the entries of a vacante's Actividad (PLAN D70).</summary>
public enum OpeningEventKind
{
    Created,
    Edited,
    StatusChanged,
    RecruiterAssigned,
    RecruiterRemoved,
    LeadChanged,
}

/// <summary>
/// DATA_MODEL §4.1, a Vacante — <c>BD_VACANTES</c> 1:1. Shown everywhere as "Cliente – Puesto" (view
/// <c>job_opening_display</c>); <see cref="LegacyCode"/> is only for the Excel import and never displayed.
/// <see cref="GenderRequirement"/> and <see cref="AgeRange"/> are internal and never reach a client DTO.
/// </summary>
public sealed class JobOpening : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public string? LegacyCode { get; set; }
    public DateOnly RequestedAt { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ZoneId { get; set; }
    public required string PositionName { get; set; }
    public int HeadcountRequested { get; set; }
    public decimal? MonthlySalary { get; set; }
    public List<string> Benefits { get; set; } = [];
    public string? Schedule { get; set; }
    public List<Guid> PaymentBankIds { get; set; } = [];
    public Guid? EducationLevelId { get; set; }
    public int? YearsExperience { get; set; }
    public string? RequiredExperience { get; set; }
    public Gender GenderRequirement { get; set; } = Gender.Unspecified;
    public string? AgeRange { get; set; }
    public OpeningPriority Priority { get; set; } = OpeningPriority.Medium;
    public OpeningStatus Status { get; set; } = OpeningStatus.Open;
    public DateOnly? FilledAt { get; set; }
    public Guid? LeadRecruiterId { get; set; }
    public Guid? DocumentTemplateId { get; set; }
    public bool VisibleToClient { get; set; } = true;
    public string? Notes { get; set; }

    public List<JobOpeningRecruiter> Recruiters { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Días abierta: <c>(fecha cubierta ?? hoy) − fecha de solicitud</c>.</summary>
    public int DaysOpen(DateOnly today) => (FilledAt ?? today).DayNumber - RequestedAt.DayNumber;
}

/// <summary>DATA_MODEL §4.2: a recruiter on a vacante, líder or apoyo; <see cref="AssignedAt"/> is <c>FECHA_ASIGNACION</c>.</summary>
public sealed class JobOpeningRecruiter : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid JobOpeningId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly AssignedAt { get; set; }
    public bool IsLead { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §4.1b: one entry of a vacante's Actividad (append-only, like the other log tables of D53).</summary>
public sealed class JobOpeningEvent
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid JobOpeningId { get; set; }
    public OpeningEventKind Kind { get; set; }
    public OpeningStatus? FromStatus { get; set; }
    public OpeningStatus? ToStatus { get; set; }
    public string? Reason { get; set; }
    public Guid? RecruiterUserId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ActingForUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>DATA_MODEL §4.6: the vacante's thread with the client (the client side arrives with the portal, S5).</summary>
public sealed class OpeningComment : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid JobOpeningId { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? ActingForUserId { get; set; }
    public required string Body { get; set; }
    public bool VisibleToClient { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Row of view <c>platform.job_opening_display</c>: the "Cliente – Puesto" name of every vacante. Read with raw SQL — an
/// EF view mapping under the default schema breaks the model snapshot in EF 10.</summary>
public sealed record JobOpeningDisplay(Guid Id, string DisplayName);
