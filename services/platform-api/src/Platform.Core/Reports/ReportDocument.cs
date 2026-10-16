namespace Platform.Core.Reports;

/// <summary>How a cell is written: text as given; counts, averages (1 decimal) and percentages (whole) as numbers in Excel.</summary>
public enum ReportCellKind
{
    Text,
    Count,
    Average,
    Percent,
}

/// <summary>
/// One cell. <see cref="Value"/> is the number Excel stores (a percentage as a fraction: 0.25) and is already rounded the way it
/// is shown — averages to 1 decimal, percentages to whole points, half away from zero — so the screen, the Excel and the PDF
/// show the same figure (these are recruiting indicators, never amounts paid). <see cref="Display"/> is that figure as text
/// («25 %», «12.5»); a <c>null</c> value is written as «—».
/// </summary>
public sealed record ReportCell(ReportCellKind Kind, string Display, decimal? Value)
{
    public const string Empty = "—";

    public static ReportCell Text(string? text) => new(ReportCellKind.Text, string.IsNullOrEmpty(text) ? Empty : text, null);

    public static ReportCell Count(int? value) => new(ReportCellKind.Count, value?.ToString(Format) ?? Empty, value);

    public static ReportCell Average(decimal? value)
    {
        var shown = value is { } v ? Math.Round(v, 1, MidpointRounding.AwayFromZero) : (decimal?)null;
        return new(ReportCellKind.Average, shown?.ToString("0.0", Format) ?? Empty, shown);
    }

    public static ReportCell Percent(decimal? fraction)
    {
        var shown = fraction is { } f ? Math.Round(f, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        return new(ReportCellKind.Percent, shown is { } p ? $"{(p * 100).ToString("0", Format)} %" : Empty, shown);
    }

    private static readonly System.Globalization.CultureInfo Format = System.Globalization.CultureInfo.GetCultureInfo("es-MX");
}

/// <summary>A row; <see cref="Emphasis"/> marks group headers and totals (bold, shaded).</summary>
public sealed record ReportRow(IReadOnlyList<ReportCell> Cells, bool Emphasis = false);

/// <summary>One table of the report: an optional heading, its column headers and rows.</summary>
public sealed record ReportTable(string? Heading, IReadOnlyList<string> Columns, IReadOnlyList<ReportRow> Rows);

/// <summary>
/// A report as exported (SPEC §4.7 «Excel/PDF export»): title, the filters line («HEB · Mónica Soto · 01/01/2026 – 16/10/2026»)
/// and its tables. Each report maps its own DTO into this, so the files show exactly what the screen shows.
/// </summary>
public sealed record ReportDocument(string Title, string Filters, IReadOnlyList<ReportTable> Tables);

public enum ReportFormat
{
    Xlsx,
    Pdf,
}

/// <summary>Writes a <see cref="ReportDocument"/> as an Excel workbook (one sheet) or a landscape PDF.</summary>
public interface IReportRenderer
{
    byte[] Render(ReportDocument document, ReportFormat format);
}

public static class ReportFormats
{
    public static string ContentType(this ReportFormat format) => format == ReportFormat.Xlsx
        ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        : "application/pdf";

    public static string Extension(this ReportFormat format) => format == ReportFormat.Xlsx ? "xlsx" : "pdf";
}
