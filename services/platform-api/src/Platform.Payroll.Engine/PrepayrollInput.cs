namespace Platform.Payroll.Engine;

/// <summary>Payroll group parameters (`payroll_groups`, DATA_MODEL §5.1).</summary>
public sealed record GroupRules(
    int SeventhDayDivisor = 6,
    decimal SundayPremiumPct = 0.25m,
    decimal HolidayWorkedMultiplier = 2m)
{
    /// <summary>FESTIVO PRIMERA MITAD: 4 hours at triple rate per entitled holiday (SPEC D14, PLAN D43).</summary>
    public const decimal HolidayFirstHalfHours = 4m;

    public const decimal HolidayFirstHalfMultiplier = 3m;
}

/// <summary>Rates in force for the employee: the position's, or the employee's overrides (DATA_MODEL §5.2, §5.4).</summary>
public sealed record Rates(
    decimal DailySalaryPocket,
    decimal DailySalaryImss,
    decimal HourlyRate,
    decimal DaysWorkedBonus = 0m);

/// <summary>
/// The employee's attendance for the period, already summarized from `attendance_records` (SPEC §5.3).
/// <paramref name="HolidaysEntitled"/> counts holiday dates with status <c>holiday</c> (PLAN D51).
/// </summary>
public sealed record AttendanceSummary(
    decimal DaysWorked,
    bool WorkedSunday = false,
    decimal PartialHours = 0m,
    decimal OvertimeHours = 0m,
    int HolidaysEntitled = 0,
    decimal HolidaysWorked = 0m);

/// <summary>
/// An incident of the period (DATA_MODEL §5.9). Hour concepts (<see cref="PayConcept.Overtime"/>, doubles, triples,
/// <see cref="PayConcept.PartialHours"/>) and <see cref="PayConcept.HolidayWorked"/> are priced from
/// <paramref name="Quantity"/> at the line's rates; every other concept pays <paramref name="Amount"/>.
/// Deductions are given as positive amounts; a <see cref="PayConcept.ManualAdjustment"/> amount is the signed change
/// to net pay and names the concept it adjusts.
/// </summary>
public sealed record IncidentInput(
    PayConcept Concept,
    decimal Quantity = 0m,
    decimal Amount = 0m,
    PayConcept? AdjustedConcept = null,
    Guid? IncidentId = null);

/// <summary>
/// A bonus concept active in the payroll group (`payroll_group_concepts`). With <paramref name="DaysBasis"/> the bonus
/// is <c>Amount × días / DaysBasis</c> (e.g. 400 / 7 × días); without it, <paramref name="Amount"/> once per period.
/// Paid only when the employee worked at least one day. Open with the client: SPEC Q4.
/// </summary>
public sealed record GroupBonus(PayConcept Concept, decimal Amount, decimal? DaysBasis = null);

public sealed record PrepayrollInput(
    GroupRules Rules,
    Rates Rates,
    AttendanceSummary Attendance,
    IReadOnlyList<IncidentInput> Incidents,
    IReadOnlyList<GroupBonus> Bonuses);

/// <summary>One `prepayroll_line_items` row: exact, never rounded (PLAN D42).</summary>
public sealed record LineItem(
    PayConcept Concept,
    ConceptKind Kind,
    string ExportColumn,
    decimal Quantity,
    decimal UnitValue,
    decimal Amount,
    ItemSource Source,
    Guid? IncidentId = null);

/// <summary>
/// The computed Excel row. <see cref="TotalNetExact"/> is Σ earnings − Σ deductions; <see cref="TotalNet"/> (TOTAL NETO A
/// PAGAR) is that amount truncated, not rounded, to cents and is the only figure reduced to cents.
/// </summary>
public sealed record PrepayrollResult(
    IReadOnlyList<LineItem> Items,
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal TotalNetExact,
    decimal TotalNet)
{
    /// <summary>Σ of the items exported in <paramref name="column"/>, signed toward net pay.</summary>
    public decimal Column(string column) =>
        Items.Where(i => i.ExportColumn == column).Sum(i => i.Kind == ConceptKind.Deduction ? -i.Amount : i.Amount);
}
