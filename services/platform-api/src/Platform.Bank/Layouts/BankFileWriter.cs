using System.Globalization;
using ClosedXML.Excel;

namespace Platform.Bank.Layouts;

public sealed class BankFileInvalidException(IReadOnlyList<LayoutIssue> issues)
    : InvalidOperationException($"El archivo tiene {issues.Count} error(es) de validación.")
{
    public IReadOnlyList<LayoutIssue> Issues { get; } = issues;
}

/// <summary>Renders a <see cref="BankLayout{TRow}"/> to <c>.xlsx</c>. A file with any broken rule is never written.</summary>
public static class BankFileWriter
{
    public static void Write<TRow>(BankLayout<TRow> layout, IReadOnlyList<TRow> rows, DateOnly today, Stream output)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(output);

        var issues = layout.Validate(rows, today);
        if (issues.Count > 0)
        {
            throw new BankFileInvalidException(issues);
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(layout.SheetName);

        foreach (var band in layout.Bands)
        {
            var range = sheet.Range($"{band.FirstLetter}1:{band.LastLetter}1");
            if (band.FirstLetter != band.LastLetter)
            {
                range.Merge();
            }
            range.FirstCell().SetValue(band.Title);
        }

        if (layout.HeaderRow is { } headerRow)
        {
            foreach (var column in layout.Columns.Where(c => c.Header.Length > 0))
            {
                sheet.Cell($"{column.Letter}{headerRow}").SetValue(column.Header);
            }
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = layout.FirstDataRow + i;
            foreach (var column in layout.Columns)
            {
                var value = column.Value(rows[i]);
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                var cell = sheet.Cell($"{column.Letter}{rowNumber}");
                if (column.Kind == CellKind.Number)
                {
                    cell.SetValue(decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture));
                    if (column.NumberFormat is { } format)
                    {
                        cell.Style.NumberFormat.Format = format;
                    }
                }
                else
                {
                    cell.Style.NumberFormat.Format = "@";
                    cell.SetValue(value);
                }
            }
        }

        foreach (var extra in layout.ExtraSheets)
        {
            var extraSheet = workbook.Worksheets.Add(extra.Name);
            for (var i = 0; i < extra.Lines.Count; i++)
            {
                if (extra.Lines[i].Length > 0)
                {
                    extraSheet.Cell(i + 1, 1).SetValue(extra.Lines[i]);
                }
            }
        }

        workbook.SaveAs(output);
    }
}
