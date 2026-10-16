using Platform.Core.Abstractions;
using Platform.Payroll.Engine;

namespace Platform.Payroll.Groups;

/// <summary>Postgres enum <c>period_type</c>.</summary>
public enum PeriodType
{
    Weekly,
    Biweekly,
}

/// <summary>Postgres enum <c>concept_calc</c> (DATA_MODEL §5.3).</summary>
public enum ConceptCalc
{
    FixedAmount,
    QtyTimesHourlyRate,
    QtyTimesHourlyRateX2,
    QtyTimesHourlyRateX3,
    QtyTimesDailySalaryX2,
    PercentOfDailySalary,
    PerDayWorked,
    Manual,
}

/// <summary>
/// DATA_MODEL §5.1, a client × scheme («ECOVIA — Validadores 12 h»): when its periods end, the shift its partial hours are
/// measured against, the daily lock of attendance and the group rules of the engine (D63). «Today» of the group is in
/// <see cref="TimeZone"/>.
/// </summary>
public sealed class PayrollGroup : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid ClientId { get; set; }
    public required string Name { get; set; }
    public PeriodType PeriodType { get; set; } = PeriodType.Weekly;
    /// <summary>The weekday the period ends: 0 = domingo … 6 = sábado.</summary>
    public short CutoffWeekday { get; set; }
    public decimal ShiftHours { get; set; } = 12m;
    public TimeOnly AttendanceLockTime { get; set; } = new(23, 59);
    public short SeventhDayDivisor { get; set; } = 6;
    public decimal SundayPremiumPct { get; set; } = 0.25m;
    public decimal HolidayWorkedMultiplier { get; set; } = 2m;
    public string? ScheduleDescription { get; set; }
    public string TimeZone { get; set; } = ClockExtensions.DefaultTimeZone;
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §5.2, a puesto of a group (VALIDADOR, SUPERVISOR). Its rates live in <see cref="PositionRate"/> rows with
/// validity dates (PLAN E12): an employee points at the puesto, which never changes when the rates do.
/// </summary>
public sealed class Position : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PayrollGroupId { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// The rates of a puesto from <see cref="ValidFrom"/> until <see cref="ValidTo"/> (open = current). A period uses the rates
/// valid at its end date (DATA_MODEL §5.2).
/// </summary>
public sealed class PositionRate : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PositionId { get; set; }
    /// <summary>SD BOLSILLO / «sueldo real», 428.57.</summary>
    public decimal DailySalaryPocket { get; set; }
    /// <summary>SD IMSS, 278.80.</summary>
    public decimal DailySalaryImss { get; set; }
    /// <summary>COSTO X HRA, 35.71 (4 decimals).</summary>
    public decimal HourlyRate { get; set; }
    /// <summary>BONO DIAS TRABAJADOS, 450.</summary>
    public decimal DaysWorkedBonus { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Rates ToRates() => new(DailySalaryPocket, DailySalaryImss, HourlyRate, DaysWorkedBonus);
}

/// <summary>DATA_MODEL §5.3 <c>pay_concepts</c>: reference data written by the migration, codes = the engine's <see cref="PayConcept"/>.</summary>
public sealed class PayConceptRow
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string Code { get; set; }
    public required string NameEs { get; set; }
    public ConceptKind Kind { get; set; }
    public ConceptCalc Calc { get; set; }
    /// <summary>For <see cref="ConceptCalc.PerDayWorked"/> the days basis (400 / <b>7</b> × días); otherwise a rate.</summary>
    public decimal? DefaultRate { get; set; }
    public decimal? DefaultAmount { get; set; }
    public string? ExportColumn { get; set; }
    public short SortOrder { get; set; }
    public bool IsSystem { get; set; }
}

/// <summary>
/// DATA_MODEL §5.3 <c>payroll_group_concepts</c>: whether a configurable concept (the bonuses, descanso laborado) pays in the
/// group, and its amount / days basis there. Every bonus starts inactive until SPEC Q4 is answered (D63).
/// </summary>
public sealed class PayrollGroupConcept
{
    public Guid PayrollGroupId { get; set; }
    public Guid PayConceptId { get; set; }
    public bool IsActive { get; set; }
    public decimal? RateOverride { get; set; }
    public decimal? AmountOverride { get; set; }
}
