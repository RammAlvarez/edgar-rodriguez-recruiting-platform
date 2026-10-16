using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using Platform.Core.Reports;

namespace Platform.Infrastructure.Reports;

/// <summary>
/// Excel with ClosedXML, PDF with PDFsharp/MigraDoc 6.2.4 — MIT, pinned: later versions are AGPL (ARCHITECTURE §2 exports,
/// PLAN D81). Numbers go to Excel as numbers with the format the screen uses, so a clerk can sum them; the PDF prints
/// <see cref="ReportCell.Display"/> in the embedded Lato font, so it looks the same on every server.
/// </summary>
public sealed class ReportRenderer : IReportRenderer
{
    static ReportRenderer() => GlobalFontSettings.FontResolver = new EmbeddedFontResolver();

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

    // ---- PDF (MigraDoc) ----

    private const string Font = "Lato";
    private static readonly Color HeaderShade = Color.FromRgb(0xE8, 0xEA, 0xF0);
    private static readonly Color RowShade = Color.FromRgb(0xF3, 0xF4, 0xF8);
    private static readonly Color Rule = Color.FromRgb(0xD5, 0xD5, 0xD5);
    private static readonly Color Muted = Color.FromRgb(0x61, 0x61, 0x61);

    private static byte[] Pdf(ReportDocument report)
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = Font;
        normal.Font.Size = 8;

        var section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        // Letter landscape, given as an explicit size: MigraDoc 6 does not apply PageFormat / Orientation to a cloned setup.
        section.PageSetup.PageWidth = Unit.FromInch(11);
        section.PageSetup.PageHeight = Unit.FromInch(8.5);
        var margin = Unit.FromPoint(28);
        section.PageSetup.TopMargin = section.PageSetup.BottomMargin = margin;
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = margin;
        var width = Unit.FromInch(11).Point - 2 * margin.Point;

        var title = section.AddParagraph(report.Title);
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        var filters = section.AddParagraph(report.Filters);
        filters.Format.Font.Color = Muted;
        filters.Format.SpaceAfter = Unit.FromPoint(8);

        foreach (var table in report.Tables)
        {
            if (table.Heading is { } heading)
            {
                var h = section.AddParagraph(heading);
                h.Format.Font.Size = 10;
                h.Format.Font.Bold = true;
                h.Format.SpaceBefore = Unit.FromPoint(6);
                h.Format.SpaceAfter = Unit.FromPoint(4);
            }
            Table(section, table, width);
            section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(8);
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void Table(Section section, ReportTable data, double width)
    {
        var table = section.AddTable();
        table.Borders.Width = 0;
        // A header sits over its numbers: right-aligned when the column's first data cell is a number.
        var numeric = Enumerable.Range(0, data.Columns.Count)
            .Select(i => data.Rows.FirstOrDefault(r => !r.Emphasis)?.Cells.ElementAtOrDefault(i)?.Kind is { } k && k != ReportCellKind.Text)
            .ToList();
        var shares = data.Columns.Count + 1; // the first column is twice as wide
        for (var i = 0; i < data.Columns.Count; i++)
        {
            var column = table.AddColumn(Unit.FromPoint(width * (i == 0 ? 2 : 1) / shares));
            column.LeftPadding = column.RightPadding = Unit.FromPoint(3);
        }

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.TopPadding = header.BottomPadding = Unit.FromPoint(3);
        header.Shading.Color = HeaderShade;
        header.Format.Font.Bold = true;
        for (var i = 0; i < data.Columns.Count; i++)
            Cell(header.Cells[i], data.Columns[i], numeric[i]);

        foreach (var r in data.Rows)
        {
            var row = table.AddRow();
            row.TopPadding = row.BottomPadding = Unit.FromPoint(3);
            row.Borders.Bottom.Width = 0.5;
            row.Borders.Bottom.Color = Rule;
            if (r.Emphasis)
            {
                row.Shading.Color = RowShade;
                row.Format.Font.Bold = true;
            }
            for (var i = 0; i < r.Cells.Count; i++)
                Cell(row.Cells[i], r.Cells[i].Display, r.Cells[i].Kind != ReportCellKind.Text);
        }
    }

    private static void Cell(Cell cell, string text, bool right)
    {
        var p = cell.AddParagraph(text);
        p.Format.Alignment = right ? ParagraphAlignment.Right : ParagraphAlignment.Left;
    }
}
