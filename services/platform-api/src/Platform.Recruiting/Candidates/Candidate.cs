using Platform.Core.Abstractions;
using Platform.Core.People;

namespace Platform.Recruiting.Candidates;

/// <summary>Postgres enum <c>candidate_stage</c> (SPEC §4.3).</summary>
public enum CandidateStage
{
    New,
    Contacted,
    Interview,
    Suitable,
    NotSuitable,
    Hired,
    Discarded,
}

/// <summary>Postgres enum <c>interview_outcome</c>.</summary>
public enum InterviewOutcome
{
    Pending,
    Attended,
    NoShow,
}

/// <summary>
/// DATA_MODEL §4.3: one application = person × vacante (<c>BD_CANDIDATOS</c>). <see cref="IsSuitable"/>, <see cref="IsHired"/>
/// and <see cref="HiredAt"/> follow <see cref="Stage"/> and are written only by the stage machine (PLAN D72).
/// </summary>
public sealed class Candidate : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public string? LegacyCode { get; set; }
    public DateOnly CapturedAt { get; set; }
    public Guid PersonId { get; set; }
    public Guid JobOpeningId { get; set; }
    public Guid? RecruiterId { get; set; }
    public CandidateSource? Source { get; set; }
    public CandidateStage Stage { get; set; } = CandidateStage.New;
    public bool? IsSuitable { get; set; }
    public bool IsHired { get; set; }
    public DateOnly? HiredAt { get; set; }
    public decimal? OfferedSalary { get; set; }
    public string? Comments { get; set; }
    public string? DiscardReason { get; set; }
    /// <summary>Set by «Convertir en empleado» (S7); the FK arrives with <c>employees</c> (PLAN D72).</summary>
    public Guid? EmployeeId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Días de proceso: <c>(fecha de ingreso ?? hoy) − fecha de captura</c>.</summary>
    public int DaysInProcess(DateOnly today) => (HiredAt ?? today).DayNumber - CapturedAt.DayNumber;
}

/// <summary>DATA_MODEL §4.4: every stage move, for the Historial, the Kanban audit and report 4.</summary>
public sealed class CandidateStageChange
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid CandidateId { get; set; }
    public CandidateStage? FromStage { get; set; }
    public CandidateStage ToStage { get; set; }
    public Guid? ChangedBy { get; set; }
    public Guid? ActingForUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Note { get; set; }
}

/// <summary>DATA_MODEL §4.4b: a rich-text note, sanitized on the server (PLAN D74). Internal only.</summary>
public sealed class CandidateNote : IStamped, ISoftDeletable
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid CandidateId { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? ActingForUserId { get; set; }
    public required string ContentHtml { get; set; }
    public string? ContentJson { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §4.5. An interview does not move the stage by itself (PLAN D75).</summary>
public sealed class Interview : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid CandidateId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public Guid? InterviewerId { get; set; }
    public string? Location { get; set; }
    public InterviewOutcome Outcome { get; set; } = InterviewOutcome.Pending;
    public string? Notes { get; set; }
    public Guid? ActingForUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
