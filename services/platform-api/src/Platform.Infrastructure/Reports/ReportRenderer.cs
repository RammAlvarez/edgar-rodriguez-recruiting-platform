using ClosedXML.Excel;
using Platform.Core.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Platform.Infrastructure.Reports;

/// <summary>
/// Excel with ClosedXML, PDF with QuestPDF (ARCHITECTURE §2 exports). Numbers go to Excel as numbers with the format the screen
/// uses, so a clerk can sum them; the PDF prints <see cref="ReportCell.Display"/>. QuestPDF runs under its Community license
/// (PLAN P9).
/// </summary>
public sealed class ReportRenderer : IReportRenderer
{
    static ReportRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(ReportDocument document, ReportFormat format) =>
        format == ReportFormat.Xlsx ? Excel(document) : Pdf(document);

    private static byte[] Excel(ReportDocument document)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName(document.Title));
        sheet.Cell(1, 1).SetValue(document.Title).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).SetValue(document.Filters).Style.Font.SetFontColor(XLColor.Gray);
        var row = 4;
        foreach (var table in document.Tables)
        {
            if (table.Heading is { } heading)
                sheet.Cell(row++, 1).SetValue(heading).Style.Font.SetBold();
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var header = sheet.Cell(row, c + 1);
                header.SetValue(table.Columns[c]);
                header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E8EAF0"));
            }
            row++;
            foreach (var r in table.Rows)
            {
                for (var c = 0; c < r.Cells.Count; c++)
                {
                    var cell = sheet.Cell(row, c + 1);
                    Write(cell, r.Cells[c]);
                    if (r.Emphasis)
                        cell.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F3F4F8"));
                }
                row++;
            }
            row++;
        }
        sheet.Columns().AdjustToContents(4, row, 8, 60);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Write(IXLCell cell, ReportCell value)
    {
        if (value.Kind == ReportCellKind.Text || value.Value is null)
        {
            cell.SetValue(value.Display);
            if (value.Kind != ReportCellKind.Text)
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            return;
        }
        cell.SetValue(value.Value.Value);
        cell.Style.NumberFormat.Format = value.Kind switch
        {
            ReportCellKind.Count => "0",
            ReportCellKind.Average => "0.0",
            _ => "0%",
        };
    }

    /// <summary>Excel sheet names: at most 31 characters, none of <c>: \ / ? * [ ]</c>.</summary>
    private static string SheetName(string title)
    {
        var clean = new string(title.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray());
        return clean.Length <= 31 ? clean : clean[..31];
    }

    private static byte[] Pdf(ReportDocument document) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.Letter.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(t => t.FontSize(8));
            page.Header().PaddingBottom(8).Column(c =>
            {
                c.Item().Text(document.Title).FontSize(14).Bold();
                c.Item().Text(document.Filters).FontColor(Colors.Grey.Darken1);
            });
            page.Content().Column(column =>
            {
                foreach (var table in document.Tables)
                    column.Item().PaddingBottom(12).Column(section =>
                    {
                        if (table.Heading is { } heading)
                            section.Item().PaddingBottom(4).Text(heading).FontSize(10).Bold();
                        section.Item().Table(t => Table(t, table));
                    });
            });
            page.Footer().AlignRight().Text(t =>
            {
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static void Table(TableDescriptor t, ReportTable table)
    {
        t.ColumnsDefinition(c =>
        {
            for (var i = 0; i < table.Columns.Count; i++)
                c.RelativeColumn(i == 0 ? 2 : 1);
        });
        // A header sits over its numbers: right-aligned when the column's first data cell is a number.
        var numeric = Enumerable.Range(0, table.Columns.Count)
            .Select(i => table.Rows.FirstOrDefault(r => !r.Emphasis)?.Cells.ElementAtOrDefault(i)?.Kind is { } k && k != ReportCellKind.Text)
            .ToList();
        t.Header(h =>
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var cell = h.Cell().Background("#E8EAF0").Padding(3);
                (numeric[i] ? cell.AlignRight() : cell).Text(table.Columns[i]).Bold();
            }
        });
        foreach (var row in table.Rows)
            for (var i = 0; i < row.Cells.Count; i++)
            {
                var cell = row.Cells[i];
                var box = t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                if (row.Emphasis)
                    box = box.Background("#F3F4F8");
                box = box.Padding(3);
                if (cell.Kind != ReportCellKind.Text)
                    box = box.AlignRight();
                var text = box.Text(cell.Display);
                if (row.Emphasis)
                    text.Bold();
            }
    }
}
