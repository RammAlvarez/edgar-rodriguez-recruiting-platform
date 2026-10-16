using Platform.Core.Abstractions;
using Platform.Core.Catalogs;

namespace Platform.Payroll.Staff;

/// <summary>Postgres enum <c>employee_status</c>: the status today, kept from the status periods (DATA_MODEL §5.4b).</summary>
public enum EmployeeStatus
{
    Active,
    Vacation,
    SickLeave,
    Suspended,
    Terminated,
}

/// <summary>Postgres enum <c>card_request_status</c>.</summary>
public enum CardRequestStatus
{
    Queued,
    InBatch,
    Uploaded,
    Received,
    Cancelled,
}

/// <summary>Postgres enum <c>attendance_status</c> (DATA_MODEL §2).</summary>
public enum AttendanceStatus
{
    Present,
    Partial,
    Absent,
    RestDay,
    RestDayWorked,
    Holiday,
    HolidayWorked,
    SickLeave,
    LeavePaid,
    LeaveUnpaid,
    Vacation,
}

/// <summary>
/// DATA_MODEL §5.4, one employment: a person working in a payroll group with a puesto and a turno. A baja ends it; a
/// reingreso is a new row (one active employment per person and group). The pay goes to the person's primary account
/// (PLAN E12: no copy of it here).
/// </summary>
public sealed class Employee : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PersonId { get; set; }
    public Guid PayrollGroupId { get; set; }
    public Guid PositionId { get; set; }
    /// <summary>«C/DÍA», «C/NOCHE» or any custom turno.</summary>
    public required string Shift { get; set; }
    public DateOnly HiredAt { get; set; }
    public DateOnly? TerminatedAt { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public decimal? DailySalaryPocketOverride { get; set; }
    public decimal? DailySalaryImssOverride { get; set; }
    public decimal? HourlyRateOverride { get; set; }
    public decimal? DaysWorkedBonusOverride { get; set; }
    public Guid? CandidateId { get; set; }
    /// <summary>COMENTARIOS of the Excel.</summary>
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §5.4b: Vacaciones, Incapacidad, Suspensión or Baja over dates. No two overlap (exclusion constraint); a Baja
/// has no end. «Reactivar» moves <see cref="EndDate"/> back, so the freed days can hold another period.
/// </summary>
public sealed class EmployeeStatusPeriod : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; set; }
    public EmployeeStatus Status { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>Motivo: of a suspension, or of a baja from <see cref="StaffRules.TerminationReasons"/>.</summary>
    public string? Reason { get; set; }
    /// <summary>Folio IMSS of an incapacidad — no medical detail is stored.</summary>
    public string? Reference { get; set; }
    public DateTimeOffset? EndedEarlyAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>DATA_MODEL §5.5b «Tarjetas por solicitar»: an employee without an account waits here for an alta file (S10).</summary>
public sealed class CardRequest : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; set; }
    public RegistrationBank Bank { get; set; }
    public CardRequestStatus Status { get; set; } = CardRequestStatus.Queued;
    public Guid? RegistrationBatchRowId { get; set; }
    public byte[]? CardNumberEnc { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §5.6: where and when an employee is expected — recurring on weekdays (1 = lunes … 7 = domingo) or one-off on a
/// date. The Calendario expands them by date (SPEC §5.2).
/// </summary>
public sealed class WorkSchedule : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; set; }
    public Guid ClientSiteId { get; set; }
    public short[] Weekdays { get; set; } = [];
    public DateOnly? OneOffDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string? JobDescription { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §5.7, one day of one employee. Created in S7 for the days a status period covers (locked, PLAN D82); capture,
/// the lock job and unlocks are S8.
/// </summary>
public sealed class AttendanceRecord
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public decimal? HoursWorked { get; set; }
    public decimal OvertimeHours { get; set; }
    public Guid? ClientSiteId { get; set; }
    public bool IsLocked { get; set; }
    public bool IsAutoAbsent { get; set; }
    /// <summary>The status period that wrote the day; ending the period early removes only these.</summary>
    public Guid? StatusPeriodId { get; set; }
    public Guid? RecordedBy { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string? Note { get; set; }
}
