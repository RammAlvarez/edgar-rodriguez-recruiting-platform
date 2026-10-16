using System.Globalization;
using ClosedXML.Excel;
using Platform.Bank.Layouts;

namespace Platform.Tests.Bank;

/// <summary>
/// The four bank files of <c>examples/</c> (read at run time, never copied): their rows go through the layouts, the
/// rows the bank would reject are exactly the ones found in the client's data, and the written files have the same
/// sheets, headers and cell layout as the originals.
/// </summary>
public sealed class BankFileExamplesTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    // SPEI codes seen in the client's files plus the DATA_MODEL §3.8 seed.
    private static readonly HashSet<string> BankCodes =
    [
        "002", "012", "014", "019", "021", "030", "036", "042", "044", "058", "059", "060", "062", "072", "127", "137",
        "138", "167", "646", "722", "728",
    ];

    [Fact]
    public void Azteca_master_rejects_exactly_the_bad_rows_and_writes_the_rest_cell_for_cell()
    {
        using var example = Open("Lo HFC MAESTRO 14AGOSTO.xlsx");
        var sheet = example.Worksheet(1);
        var excelRows = Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1).ToList();
        var payer = new AztecaPayingAccount(Text(sheet.Cell("A2")), Text(sheet.Cell("B2")), Text(sheet.Cell("C2")));
        var rows = excelRows.Select(r => new AztecaPaymentRow(Text(sheet.Cell($"G{r}")),
            (AccountType)int.Parse(Text(sheet.Cell($"H{r}")), CultureInfo.InvariantCulture), Text(sheet.Cell($"I{r}")),
            Text(sheet.Cell($"J{r}")), 1.00m, ChargeLegend: Text(sheet.Cell($"D{r}")))).ToList();
        var layout = AztecaMassPayment.Layout(payer, BankCodes);

        var issues = layout.Validate(rows, Today);

        // 15: 16 digits typed as CLABE; 123, 143: 17-digit CLABEs; 153: 44-char name; 156: bank code typed «azteca».
        Assert.Equal([15, 123, 143, 153, 156], issues.Select(i => excelRows[i.Line - 1]).Distinct());
        Assert.Contains(issues, i => excelRows[i.Line - 1] == 156 && i.Field == "CLAVE DEL BANCO RECEPTOR");
        Assert.Contains(issues, i => excelRows[i.Line - 1] == 153 && i.Field == "NOMBRE DEL BENEFICIARIO");

        var keep = excelRows.Where(r => r is not (15 or 123 or 143 or 153 or 156)).ToList();
        using var written = WriteAndOpen(layout, keep.Select(r => rows[excelRows.IndexOf(r)]).ToList());
        var output = written.Worksheet("Hoja1");
        AssertSameCells(sheet, output, "A1:O1");
        for (var k = 0; k < keep.Count; k++)
        {
            // Names are written trimmed (the master has a few trailing spaces).
            foreach (var letter in "ABCDFGHIJKLMNO")
            {
                Assert.Equal(Text(sheet.Cell($"{letter}{keep[k]}")).Trim(), Text(output.Cell($"{letter}{k + 2}")));
            }
            Assert.Equal(1.00, output.Cell($"E{k + 2}").GetDouble());
        }
        Assert.Equal(keep.Count + 1, output.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public void Payment_type_is_baz_transfer_only_for_Azteca_accounts_as_in_the_master()
    {
        using var example = Open("Lo HFC MAESTRO 14AGOSTO.xlsx");
        var sheet = example.Worksheet(1);
        foreach (var r in Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1).Where(r => r != 156))
        {
            Assert.Equal(Text(sheet.Cell($"O{r}")), AztecaMassPayment.PaymentType(Text(sheet.Cell($"G{r}"))));
        }
    }

    [Fact]
    public void Frequent_accounts_file_is_reproduced_exactly()
    {
        using var example = Open("cuentasfrec21sept.xlsx");
        var sheet = example.Worksheet(1);
        var rows = new[] { 1, 2 }.Select(r => new FrequentAccountRow(Text(sheet.Cell($"A{r}")), Text(sheet.Cell($"B{r}")),
            AccountType.DebitCard, Text(sheet.Cell($"D{r}")), Text(sheet.Cell($"F{r}")))).ToList();

        using var written = WriteAndOpen(AztecaFrequentAccounts.Layout(BankCodes), rows);
        AssertSameCells(sheet, written.Worksheet("Hoja1"), "A1:J3");
    }

    [Fact]
    public void Banorte_example_rows_break_exactly_the_bank_rules_found_in_the_client_file()
    {
        using var example = Open("Alta_Nomina_Masiva Banorte 21Ago-1.xlsx");
        var sheet = example.Worksheet("Masiva_xls");
        var excelRows = Enumerable.Range(3, 93).ToList();
        var rows = excelRows.Select(r => BanorteRow(sheet, r)).ToList();

        var issues = BanorteMassRegistration.Layout(new BanorteBatchSettings()).Validate(rows, Today);
        int[] Rows(string field, string? contains = null) => issues
            .Where(i => i.Field == field && (contains is null || i.Message.Contains(contains, StringComparison.Ordinal)))
            .Select(i => excelRows[i.Line - 1]).Distinct().Order().ToArray();

        Assert.Equal(90, Rows("Número de empleado").Length);
        Assert.Equal([4, 16], Rows("CURP", "extranjero"));
        Assert.Equal([44], Rows("CURP", "estructura"));
        Assert.Equal([13], Rows("RFC"));
        Assert.Equal([19, 38, 81], Rows("Calle"));
        Assert.Equal([12, 35, 38, 42, 76, 84, 85, 87, 88, 90, 91, 93, 94, 95], Rows("Correo electrónico"));
        Assert.DoesNotContain(issues, i => excelRows[i.Line - 1] is 3 or 5);
    }

    [Fact]
    public void Banorte_file_has_the_portal_sheet_bands_headers_and_catalogs()
    {
        using var example = Open("Alta_Nomina_Masiva Banorte 21Ago-1.xlsx");
        var sheet = example.Worksheet("Masiva_xls");
        int[] valid = [3, 5];

        using var written = WriteAndOpen(BanorteMassRegistration.Layout(new BanorteBatchSettings()),
            valid.Select(r => BanorteRow(sheet, r)).ToList());
        var output = written.Worksheet("Masiva_xls");

        AssertSameCells(sheet, output, "A1:P2");
        Assert.Equal(sheet.MergedRanges.Select(m => m.RangeAddress.ToString()).Order(),
            output.MergedRanges.Select(m => m.RangeAddress.ToString()).Order());
        for (var k = 0; k < valid.Length; k++)
        {
            foreach (var letter in "BCDEFGHIJKLMNP")
            {
                Assert.Equal(BankText.Normalize(Text(sheet.Cell($"{letter}{valid[k]}"))),
                    BankText.Normalize(Text(output.Cell($"{letter}{k + 3}"))));
            }
            Assert.Equal(Text(sheet.Cell($"G{valid[k]}")), Text(output.Cell($"G{k + 3}")));
        }
        Assert.Equal(100_000, output.Cell("C3").GetDouble());
        AssertSameCells(example.Worksheet("Catálogos"), written.Worksheet("Catálogos"), "A1:A30");
    }

    [Fact]
    public void Banregio_example_writes_with_the_same_headers_and_catches_the_birth_date_typo()
    {
        using var example = Open("ALTA BANREGIO EARL 19AGO26.xlsx");
        var sheet = example.Worksheet("Hoja1");
        var excelRows = Enumerable.Range(2, 6).ToList();
        var rows = excelRows.Select(r => BanregioRow(sheet, r)).ToList();
        var layout = BanregioRegistration.Layout();

        // Row 5: FechaNac 29/09/1989, CURP EIGF890928… (28 sep).
        var issues = layout.Validate(rows, Today);
        Assert.Equal([(5, "FechaNac")], issues.Select(i => (excelRows[i.Line - 1], i.Field)));

        var keep = excelRows.Where(r => r != 5).ToList();
        using var written = WriteAndOpen(layout, keep.Select(r => rows[excelRows.IndexOf(r)]).ToList());
        var output = written.Worksheet("Hoja1");
        AssertSameCells(sheet, output, "A1:Q1");
        for (var k = 0; k < keep.Count; k++)
        {
            foreach (var letter in "ABCDEFGHIJKLMNOPQ")
            {
                Assert.Equal(BankText.Normalize(Text(sheet.Cell($"{letter}{keep[k]}"))),
                    BankText.Normalize(Text(output.Cell($"{letter}{k + 2}"))));
            }
        }
    }

    private static BanorteRegistrationRow BanorteRow(IXLWorksheet s, int r) => new(
        Text(s.Cell($"B{r}")), Text(s.Cell($"D{r}")), Text(s.Cell($"E{r}")), Text(s.Cell($"F{r}")), Text(s.Cell($"H{r}")),
        Text(s.Cell($"I{r}")), Text(s.Cell($"J{r}")), Text(s.Cell($"K{r}")), Text(s.Cell($"N{r}")), Text(s.Cell($"P{r}")));

    private static BanregioRegistrationRow BanregioRow(IXLWorksheet s, int r)
    {
        string T(string letter) => Text(s.Cell($"{letter}{r}"));
        return new(T("A"), T("B"), T("C"), T("D"), T("E"), T("F"), T("G"), T("H"), T("I"), T("J"), T("K"),
            DateOnly.ParseExact(T("L"), "dd/MM/yyyy", CultureInfo.InvariantCulture),
            T("M").Equals("FEMENINO", StringComparison.OrdinalIgnoreCase) ? Gender.Female : Gender.Male,
            T("N"), T("O"), T("P"), T("Q"));
    }

    /// <summary>A cell as the person typed it: text as is, numbers without decimals noise, dates dd/mm/yyyy.</summary>
    private static string Text(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return "";
        }
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.DateTime => value.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            XLDataType.Number => value.GetNumber().ToString(CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static void AssertSameCells(IXLWorksheet expected, IXLWorksheet actual, string range)
    {
        foreach (var cell in expected.Range(range).Cells(false))
        {
            var other = actual.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber);
            Assert.True(Text(cell) == Text(other),
                $"{expected.Name}!{cell.Address}: expected «{Text(cell)}», got «{Text(other)}»");
        }
    }

    private static XLWorkbook WriteAndOpen<TRow>(BankLayout<TRow> layout, IReadOnlyList<TRow> rows)
    {
        var stream = new MemoryStream();
        BankFileWriter.Write(layout, rows, Today, stream);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    private static XLWorkbook Open(string file)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "examples", file);
            if (File.Exists(path))
            {
                return new XLWorkbook(path);
            }
        }
        throw new FileNotFoundException($"examples/{file} not found above {AppContext.BaseDirectory}");
    }
}
