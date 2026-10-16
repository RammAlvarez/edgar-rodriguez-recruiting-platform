using Platform.Payroll.Engine;

namespace Platform.Tests.Payroll;

/// <summary>The SPEC §5.5 rules the SEM 38 workbook does not exercise (doubles, triples, deductions, bonuses …).</summary>
public sealed class PrepayrollCalculatorTests
{
    private static readonly Rates Validador = new(428.57m, 278.80m, 35.71m, 450m);

    private static PrepayrollResult Calc(AttendanceSummary attendance, IReadOnlyList<IncidentInput>? incidents = null,
        IReadOnlyList<GroupBonus>? bonuses = null, Rates? rates = null) =>
        PrepayrollCalculator.Calculate(new PrepayrollInput(new GroupRules(), rates ?? Validador, attendance,
            incidents ?? [], bonuses ?? []));

    [Fact]
    public void Seventh_day_is_stored_with_six_decimals_and_only_the_payable_is_cut_to_cents()
    {
        var r = Calc(new AttendanceSummary(DaysWorked: 1));

        Assert.Equal(71.428333m, r.Items.Single(i => i.Concept == PayConcept.SeventhDay).Amount);
        Assert.Equal(499.998333m, r.TotalNetExact);
        Assert.Equal(499.99m, r.TotalNet);
    }

    [Theory]
    [InlineData("71.4283333333", "71.428333")]
    [InlineData("71.4283339999", "71.428333")]
    [InlineData("-10.1234569", "-10.123456")]
    public void Stored_amounts_are_cut_to_six_decimals_never_rounded(string exact, string stored) =>
        Assert.Equal(decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture),
            PrepayrollCalculator.TruncateToStored(decimal.Parse(exact, System.Globalization.CultureInfo.InvariantCulture)));


    [Theory]
    [InlineData("1234.5678", "1234.56")]
    [InlineData("1234.5699", "1234.56")]
    [InlineData("0.009", "0.00")]
    [InlineData("-10.129", "-10.12")]
    public void Truncation_never_rounds(string exact, string payable) =>
        Assert.Equal(decimal.Parse(payable, System.Globalization.CultureInfo.InvariantCulture),
            PrepayrollCalculator.TruncateToCents(decimal.Parse(exact, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Sunday_premium_is_a_quarter_of_the_daily_salary_once()
    {
        var r = Calc(new AttendanceSummary(6, WorkedSunday: true));
        Assert.Equal(107.1425m, r.Column("PRIMA DOMINICAL"));
    }

    [Fact]
    public void Festivo_primera_mitad_is_four_hours_at_triple_rate_per_entitled_holiday()
    {
        Assert.Equal(428.52m, Calc(new AttendanceSummary(5, HolidaysEntitled: 1)).Column("FESTIVO PRIMERA MITAD"));
        Assert.Equal(585.60m, Calc(new AttendanceSummary(5, HolidaysEntitled: 1),
            rates: new Rates(585.71m, 278.80m, 48.80m)).Column("FESTIVO PRIMERA MITAD"));
        Assert.Equal(857.04m, Calc(new AttendanceSummary(4, HolidaysEntitled: 2)).Column("FESTIVO PRIMERA MITAD"));
    }

    [Fact]
    public void Festivo_laborado_pays_double_daily_salary_per_day()
    {
        var r = Calc(new AttendanceSummary(6, HolidaysWorked: 1));
        Assert.Equal(857.14m, r.Column("FESTIVO LABORADO"));
    }

    [Fact]
    public void Overtime_doubles_and_triples_are_priced_from_hours_at_the_line_rate()
    {
        var r = Calc(new AttendanceSummary(6, OvertimeHours: 2), [
            new IncidentInput(PayConcept.OvertimeDouble, Quantity: 3),
            new IncidentInput(PayConcept.OvertimeTriple, Quantity: 1.5m),
        ]);

        Assert.Equal(71.42m, r.Column("HORAS EXTRA"));
        Assert.Equal(214.26m, r.Column("TIEMPO EXTRA DOBLE"));
        Assert.Equal(160.695m, r.Column("TIEMPO EXTRA TRIPLE"));
    }

    [Fact]
    public void Deductions_subtract_and_amount_incidents_pay_their_amount()
    {
        var loan = Guid.NewGuid();
        var r = Calc(new AttendanceSummary(6), [
            new IncidentInput(PayConcept.RestDayWorked, Amount: 500),
            new IncidentInput(PayConcept.Retroactive, Amount: 428.57m),
            new IncidentInput(PayConcept.Deduction, Amount: 150),
            new IncidentInput(PayConcept.LoanInstallment, Amount: 200, IncidentId: loan),
        ]);

        Assert.Equal(350m, r.TotalDeductions);
        Assert.Equal(-350m, r.Column("DESCUENTO"));
        Assert.Equal(2571.42m + 428.57m + 500m + 428.57m, r.GrossEarnings);
        Assert.Equal(r.GrossEarnings - 350m, r.TotalNetExact);
        Assert.Equal(loan, r.Items.Single(i => i.Concept == PayConcept.LoanInstallment).IncidentId);
    }

    [Fact]
    public void A_manual_adjustment_lands_in_the_column_it_adjusts_with_its_sign()
    {
        var r = Calc(new AttendanceSummary(6), [
            new IncidentInput(PayConcept.ManualAdjustment, Amount: -100, AdjustedConcept: PayConcept.Salary),
        ]);

        var item = r.Items.Single(i => i.Concept == PayConcept.ManualAdjustment);
        Assert.Equal(("SUELDO", ItemSource.Manual), (item.ExportColumn, item.Source));
        Assert.Equal(2571.42m - 100m, r.Column("SUELDO"));
        Assert.Equal(2571.42m + 428.57m - 100m, r.TotalNetExact);
    }

    [Fact]
    public void Group_bonuses_pay_fixed_or_per_day_and_nothing_without_worked_days()
    {
        GroupBonus[] bonuses =
        [
            new(PayConcept.BonusDaysWorked, 0),
            new(PayConcept.BonusPunctuality, 400, DaysBasis: 7),
        ];

        var r = Calc(new AttendanceSummary(6), bonuses: bonuses);
        Assert.Equal(450m, r.Column("BONO DIAS TRABAJADOS"));
        Assert.Equal(342.857142m, r.Column("BONO PUNTUALIDAD Y ASISTENCIA")); // 400 × 6 / 7, stored with 6 decimals

        Assert.Equal(0m, Calc(new AttendanceSummary(0), bonuses: bonuses).TotalNetExact);
    }

    [Fact]
    public void Items_come_out_in_Excel_column_order_without_zero_amounts()
    {
        var r = Calc(new AttendanceSummary(6, WorkedSunday: true, PartialHours: 4, HolidaysEntitled: 1),
            [new IncidentInput(PayConcept.Deduction, Amount: 10), new IncidentInput(PayConcept.RestDayWorked, Amount: 500)]);

        Assert.Equal(
            [PayConcept.Salary, PayConcept.SeventhDay, PayConcept.SundayPremium, PayConcept.PartialHours,
                PayConcept.HolidayFirstHalf, PayConcept.RestDayWorked, PayConcept.Deduction],
            r.Items.Select(i => i.Concept));
    }

    [Theory]
    [InlineData(PayConcept.Salary)]
    [InlineData(PayConcept.SeventhDay)]
    [InlineData(PayConcept.HolidayFirstHalf)]
    [InlineData(PayConcept.BonusPunctuality)]
    public void Concepts_derived_from_attendance_or_the_group_are_refused_as_incidents(PayConcept concept) =>
        Assert.Throws<ArgumentException>(() => Calc(new AttendanceSummary(6), [new IncidentInput(concept, 1, 1)]));

    [Fact]
    public void Invalid_inputs_are_refused()
    {
        Assert.Throws<ArgumentException>(() => Calc(new AttendanceSummary(-1)));
        Assert.Throws<ArgumentException>(() => Calc(new AttendanceSummary(6),
            [new IncidentInput(PayConcept.Deduction, Amount: -5)]));
        Assert.Throws<ArgumentException>(() => Calc(new AttendanceSummary(6),
            [new IncidentInput(PayConcept.ManualAdjustment, Amount: 5)]));
        Assert.Throws<ArgumentException>(() => Calc(new AttendanceSummary(6),
            bonuses: [new GroupBonus(PayConcept.Deduction, 5)]));
    }

    [Fact]
    public void Concept_codes_and_columns_match_the_pay_concepts_seed()
    {
        Assert.Equal(17, PayConcepts.All.Count);
        Assert.Equal("HOLIDAY_FIRST_HALF", PayConcept.HolidayFirstHalf.Info().Code);
        Assert.Equal(ConceptKind.Deduction, PayConcept.LoanInstallment.Info().Kind);
        Assert.All(PayConcepts.All.Where(c => c.Concept != PayConcept.ManualAdjustment),
            c => Assert.False(string.IsNullOrEmpty(c.ExportColumn)));
    }

    [Theory]
    [InlineData("2026-09-17", 2026, 38)]
    [InlineData("2026-01-01", 2026, 1)]
    [InlineData("2027-01-01", 2026, 53)]
    public void Weekly_period_number_is_the_ISO_week_of_the_end_date(string end, int year, int week)
    {
        var date = DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal((year, week), (PeriodNumbering.WeeklyYear(date), PeriodNumbering.WeeklyNumber(date)));
    }

    [Theory]
    [InlineData("2026-01-15", 1)]
    [InlineData("2026-01-31", 2)]
    [InlineData("2026-12-31", 24)]
    public void Biweekly_period_number_counts_halves_of_the_year(string end, int number) =>
        Assert.Equal(number, PeriodNumbering.BiweeklyNumber(DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture)));
}
