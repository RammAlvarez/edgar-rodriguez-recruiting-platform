using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Platform.Payroll.Engine;

namespace Platform.Tests.Payroll;

/// <summary>
/// The golden test of PLAN D41: every row of the client's <c>PRENOMINA SEM 38</c> workbook, read from <c>examples/</c>
/// (never transcribed), recomputed by the engine and compared with Excel's cached values.
/// </summary>
public sealed partial class Sem38GoldenTests
{
    private const string Workbook = "PRENOMINA SEM 38 12 HRS ECOVIA VALIDADORES.xlsx";
    private const int FirstRow = 6;
    private const int LastRow = 120;
    private const int TotalsRow = 121;
    private const double Tolerance = 1e-6;

    // Columns the engine produces, by Excel letter.
    private static readonly (string Letter, string Column)[] Checked =
    [
        ("K", "SUELDO"), ("L", "SEPTIMO DIA"), ("M", "PRIMA DOMINICAL"),
        ("N", "MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS"), ("P", "HORAS EXTRA"), ("Q", "RETROACTIVO"),
        ("R", "DESCANSO LABORADO"), ("S", "TIEMPO EXTRA DOBLE"), ("T", "FESTIVO LABORADO"), ("U", "DESCUENTO"),
    ];

    // Rows whose typed FESTIVO PRIMERA MITAD has no rule (SPEC Q12); the engine pays D14 there.
    private static readonly int[] Rows249_50 = [65, 83, 99, 117];
    private static readonly int[] Rows178_60 = [81, 85, 87];

    private sealed record Row(int Number, PrepayrollResult Result, IXLWorksheet Sheet, double SheetHolidayFirstHalf);

    [Fact]
    public void Every_column_except_festivo_primera_mitad_equals_the_sheet()
    {
        var mismatches = new List<string>();
        foreach (var row in Rows())
        {
            foreach (var (letter, column) in Checked)
            {
                var expected = Number(row.Sheet.Cell($"{letter}{row.Number}"));
                var actual = row.Result.Column(column);
                if (Math.Abs((double)actual - expected) > Tolerance)
                {
                    mismatches.Add($"{letter}{row.Number} {column}: sheet {expected}, engine {actual}");
                }
            }
            // V = SUM(K:U) includes the sheet's own O, so put it back to compare the row total.
            var sheetTotal = Number(row.Sheet.Cell($"V{row.Number}"));
            var withSheetO = row.Result.TotalNetExact - row.Result.Column(HolidayColumn) + (decimal)row.SheetHolidayFirstHalf;
            if (Math.Abs((double)withSheetO - sheetTotal) > Tolerance)
            {
                mismatches.Add($"V{row.Number}: sheet {sheetTotal}, engine with the sheet's O {withSheetO}");
            }
        }
        Assert.True(mismatches.Count == 0, string.Join('\n', mismatches));
    }

    [Fact]
    public void Festivo_primera_mitad_follows_D14_and_the_known_differences_are_exactly_those_of_D41()
    {
        var diffs = Rows()
            .Select(r => (r.Number, Sheet: r.SheetHolidayFirstHalf, Engine: r.Result.Column(HolidayColumn),
                Hourly: Number(r.Sheet.Cell($"G{r.Number}"))))
            .Where(d => Math.Abs((double)d.Engine - d.Sheet) > Tolerance)
            .ToList();

        foreach (var d in diffs)
        {
            Assert.Equal(4m * (decimal)d.Hourly * 3m, d.Engine);
        }
        Assert.Equal(34, diffs.Count(d => d.Sheet == 428.5 && d.Engine == 428.52m));
        Assert.Equal(5, diffs.Count(d => d.Sheet == 585.7 && d.Engine == 585.60m));
        Assert.Equal(Rows249_50, diffs.Where(d => d.Sheet == 249.5 && d.Engine == 428.52m).Select(d => d.Number));
        Assert.Equal(Rows178_60, diffs.Where(d => d.Sheet == 178.6 && d.Engine == 428.52m).Select(d => d.Number));
        Assert.Equal(34 + 5 + 4 + 3, diffs.Count);
    }

    [Fact]
    public void Each_line_is_exact_and_its_payable_amount_is_truncated()
    {
        foreach (var row in Rows())
        {
            var r = row.Result;
            Assert.Equal(r.Items.Where(i => i.Kind == ConceptKind.Earning).Sum(i => i.Amount)
                - r.Items.Where(i => i.Kind == ConceptKind.Deduction).Sum(i => i.Amount), r.TotalNetExact);
            Assert.Equal(decimal.Truncate(r.TotalNetExact * 100m) / 100m, r.TotalNet);
        }
    }

    [Fact]
    public void Period_totals_match_D41()
    {
        var rows = Rows();
        var sheetSum = Number(Sheet().Cell($"V{TotalsRow}"));

        // Engine (D14 for every entitled row); items carry 6 decimals, so the period sum is exact at 6 decimals.
        Assert.Equal(334_655.149146m, rows.Sum(r => r.Result.TotalNetExact));
        Assert.Equal(334_654.83m, rows.Sum(r => r.Result.TotalNet));

        // The same rows with the sheet's own FESTIVO PRIMERA MITAD reproduce Excel's total, less what cutting each item
        // to 6 decimals drops (under 1e-6 per item).
        var withSheetO = rows.Select(r =>
            r.Result.TotalNetExact - r.Result.Column(HolidayColumn) + (decimal)r.SheetHolidayFirstHalf).ToList();
        var items = rows.Sum(r => r.Result.Items.Count);
        Assert.InRange((double)withSheetO.Sum(), sheetSum - items * Tolerance, sheetSum + Tolerance);
        Assert.Equal(333_188.81m, withSheetO.Sum(PrepayrollCalculator.TruncateToCents));
    }

    private const string HolidayColumn = "FESTIVO PRIMERA MITAD";

    internal static IXLWorksheet Sheet() => new XLWorkbook(WorkbookPath()).Worksheet("SEM 38");

    private static List<Row> Rows()
    {
        var sheet = Sheet();
        Assert.Equal("TOTAL NETO A PAGAR", sheet.Cell("V5").GetString());
        Assert.True(sheet.Cell($"C{LastRow + 1}").IsEmpty());

        var rows = new List<Row>();
        for (var n = FirstRow; n <= LastRow; n++)
        {
            var cell = (string letter) => sheet.Cell($"{letter}{n}");
            Assert.False(cell("C").IsEmpty(), $"row {n} has no employee");

            var rates = new Rates(
                DailySalaryPocket: (decimal)Number(cell("J")),
                DailySalaryImss: (decimal)Number(cell("I")),
                HourlyRate: (decimal)Number(cell("G")));
            var attendance = new AttendanceSummary(
                DaysWorked: (decimal)Number(cell("F")),
                WorkedSunday: !cell("M").IsEmpty(),
                PartialHours: HourlyMultiple(cell("N"), n),
                OvertimeHours: HourlyMultiple(cell("P"), n),
                HolidaysEntitled: cell("O").IsEmpty() ? 0 : 1,
                HolidaysWorked: (decimal)Number(cell("H")));
            var incidents = cell("R").IsEmpty()
                ? []
                : new List<IncidentInput> { new(PayConcept.RestDayWorked, Amount: (decimal)Number(cell("R"))) };

            var result = PrepayrollCalculator.Calculate(
                new PrepayrollInput(new GroupRules(), rates, attendance, incidents, []));
            rows.Add(new Row(n, result, sheet, Number(cell("O"))));
        }
        return rows;
    }

    /// <summary>A cell's number as Excel cached it (never recalculated by ClosedXML); empty is 0.</summary>
    internal static double Number(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return 0;
        }
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        return value.IsBlank ? 0 : value.GetNumber();
    }

    /// <summary>The hours in an <c>=G{row}*n</c> formula (columns N and P).</summary>
    internal static decimal HourlyMultiple(IXLCell cell, int row)
    {
        if (cell.IsEmpty())
        {
            return 0m;
        }
        var match = HourlyFormula().Match(cell.FormulaA1);
        Assert.True(match.Success && match.Groups[1].Value == row.ToString(CultureInfo.InvariantCulture),
            $"{cell.Address}: unexpected formula '{cell.FormulaA1}'");
        return decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^=?G(\d+)\*(\d+(?:\.\d+)?)$")]
    private static partial Regex HourlyFormula();

    internal static string WorkbookPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "examples", Workbook);
            if (File.Exists(path))
            {
                return path;
            }
        }
        throw new FileNotFoundException($"examples/{Workbook} not found above {AppContext.BaseDirectory}");
    }
}
