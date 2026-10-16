namespace Platform.Payroll.Engine;

/// <summary>The system pay concepts of DATA_MODEL §5.3. Declaration order is the Excel column order of the period export.</summary>
public enum PayConcept
{
    Salary,
    SeventhDay,
    SundayPremium,
    PartialHours,
    HolidayFirstHalf,
    Overtime,
    Retroactive,
    RestDayWorked,
    OvertimeDouble,
    OvertimeTriple,
    HolidayWorked,
    BonusDaysWorked,
    BonusPunctuality,
    BonusProductivity,
    Deduction,
    LoanInstallment,
    ManualAdjustment,
}

public enum ConceptKind
{
    Earning,
    Deduction,
}

/// <summary>Where a line item's amount came from (`prepayroll_line_items.source`).</summary>
public enum ItemSource
{
    Attendance,
    Incident,
    Position,
    Manual,
}

/// <summary>Code, kind and Excel column of each concept (the `pay_concepts` seed).</summary>
public sealed record PayConceptInfo(PayConcept Concept, string Code, ConceptKind Kind, string ExportColumn);

public static class PayConcepts
{
    private static readonly Dictionary<PayConcept, PayConceptInfo> ByConcept = new PayConceptInfo[]
    {
        new(PayConcept.Salary, "SALARY", ConceptKind.Earning, "SUELDO"),
        new(PayConcept.SeventhDay, "SEVENTH_DAY", ConceptKind.Earning, "SEPTIMO DIA"),
        new(PayConcept.SundayPremium, "SUNDAY_PREMIUM", ConceptKind.Earning, "PRIMA DOMINICAL"),
        new(PayConcept.PartialHours, "PARTIAL_HOURS", ConceptKind.Earning, "MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS"),
        new(PayConcept.HolidayFirstHalf, "HOLIDAY_FIRST_HALF", ConceptKind.Earning, "FESTIVO PRIMERA MITAD"),
        new(PayConcept.Overtime, "OVERTIME", ConceptKind.Earning, "HORAS EXTRA"),
        new(PayConcept.Retroactive, "RETROACTIVE", ConceptKind.Earning, "RETROACTIVO"),
        new(PayConcept.RestDayWorked, "REST_DAY_WORKED", ConceptKind.Earning, "DESCANSO LABORADO"),
        new(PayConcept.OvertimeDouble, "OVERTIME_DOUBLE", ConceptKind.Earning, "TIEMPO EXTRA DOBLE"),
        new(PayConcept.OvertimeTriple, "OVERTIME_TRIPLE", ConceptKind.Earning, "TIEMPO EXTRA TRIPLE"),
        new(PayConcept.HolidayWorked, "HOLIDAY_WORKED", ConceptKind.Earning, "FESTIVO LABORADO"),
        new(PayConcept.BonusDaysWorked, "BONUS_DAYS_WORKED", ConceptKind.Earning, "BONO DIAS TRABAJADOS"),
        new(PayConcept.BonusPunctuality, "BONUS_PUNCTUALITY", ConceptKind.Earning, "BONO PUNTUALIDAD Y ASISTENCIA"),
        new(PayConcept.BonusProductivity, "BONUS_PRODUCTIVITY", ConceptKind.Earning, "BONO PRODUCTIVIDAD"),
        new(PayConcept.Deduction, "DEDUCTION", ConceptKind.Deduction, "DESCUENTO"),
        new(PayConcept.LoanInstallment, "LOAN_INSTALLMENT", ConceptKind.Deduction, "DESCUENTO"),
        // Signed change to net pay, exported in the adjusted concept's column.
        new(PayConcept.ManualAdjustment, "MANUAL_ADJUSTMENT", ConceptKind.Earning, ""),
    }.ToDictionary(i => i.Concept);

    public static IReadOnlyCollection<PayConceptInfo> All => ByConcept.Values;

    public static PayConceptInfo Info(this PayConcept concept) => ByConcept[concept];
}
