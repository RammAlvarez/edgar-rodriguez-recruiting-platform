using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Platform.Payroll.Prepayroll;

namespace Platform.Infrastructure.Reports;

/// <summary>
/// The period's Excel in the client's layout (SEM 38: title rows 3–4, headers B5:W5, data from row 6) and the receipts PDF
/// (MigraDoc 6.2.4, MIT — PLAN D81). Every amount is the screen's: cut to cents, never rounded (D42).
/// </summary>
public sealed class PrepayrollFiles : IPrepayrollFiles
{
    static PrepayrollFiles() => EmbeddedFontResolver.Install();

    private static readonly string[] Leading =
        ["Fecha ingreso", "Empleado", "PUESTO", "TURNO", "DIAS LABORADOS", "COSTO X HRA", "FESTIVO", "SD IMSS", "SD BOLSILLO"];

    private const string MoneyFormat = "#,##0.00;-#,##0.00";

    /// <summary>D76: the footers add what is shown; what is paid is each person's TOTAL NETO A PAGAR.</summary>
    public const string FooterNote =
        "Totales de columna: suma de los importes mostrados, solo informativos. Lo que se paga es el TOTAL NETO A PAGAR de cada persona.";

    public byte[] Excel(PrepayrollHeading heading, PrepayrollGridDto grid)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName(heading.PeriodLabel));
        sheet.Cell("C3").SetValue($"Archivo de Nomina {heading.ClientName} {heading.GroupName}").Style.Font.SetBold();
        sheet.Cell("F3").SetValue(heading.ScheduleDescription?.ToUpperInvariant() ?? "");
        sheet.Cell("C4").SetValue(heading.PeriodLabel).Style.Font.SetBold();
        sheet.Cell("F4").SetValue(heading.DateRange);

        const int first = 2; // column B
        var headers = Leading.Concat(grid.Columns.Select(c => PrepayrollLabels.ColumnTitle(c, grid.ShiftHours))).Append("TOTAL NETO A PAGAR").Append("COMENTARIOS").ToList();
        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(5, first + c);
            cell.SetValue(headers[c]);
            cell.Style.Font.SetBold().Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EAF0"));
        }

        var row = 6;
        foreach (var r in grid.Rows)
        {
            var c = first;
            sheet.Cell(row, c++).SetValue(r.HiredAt.ToDateTime(TimeOnly.MinValue)).Style.NumberFormat.Format = "dd/mm/yyyy";
            sheet.Cell(row, c++).SetValue(r.FullName);
            sheet.Cell(row, c++).SetValue(r.PositionName);
            sheet.Cell(row, c++).SetValue(r.Shift);
            sheet.Cell(row, c++).SetValue(r.DaysWorked);
            Number(sheet.Cell(row, c++), r.HourlyRate);
            if (r.HolidaysWorked != 0)
                sheet.Cell(row, c).SetValue(r.HolidaysWorked);
            c++;
            Number(sheet.Cell(row, c++), r.DailySalaryImss);
            Number(sheet.Cell(row, c++), r.DailySalaryPocket);
            foreach (var amount in r.Amounts)
            {
                if (amount is { } a)
                    Number(sheet.Cell(row, c), a);
                c++;
            }
            Number(sheet.Cell(row, c++), r.TotalNet).Style.Font.SetBold();
            sheet.Cell(row, c).SetValue(r.Comments ?? "");
            row++;
        }

        // Footers (D76) under the amount columns and the total.
        var amountsFrom = first + Leading.Length;
        for (var i = 0; i < grid.Footers.Count; i++)
            Number(sheet.Cell(row, amountsFrom + i), grid.Footers[i]).Style.Font.SetBold();
        Number(sheet.Cell(row, amountsFrom + grid.Footers.Count), grid.TotalNet).Style.Font.SetBold();
        sheet.Range(row, first, row, amountsFrom + grid.Footers.Count).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);
        sheet.Cell(row + 1, first + 1).SetValue(FooterNote).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        sheet.Column(first).Width = 12;
        sheet.Column(first + 1).Width = 38;
        for (var c = first + 2; c < first + headers.Count - 1; c++)
            sheet.Column(c).Width = 13;
        sheet.Column(first + headers.Count - 1).Width = 30;
        sheet.Row(5).Height = 45;
        sheet.SheetView.FreezeRows(5);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static IXLCell Number(IXLCell cell, decimal value)
    {
        cell.SetValue(value);
        cell.Style.NumberFormat.Format = MoneyFormat;
        return cell;
    }

    /// <summary>«SEMANA N° 38» → «SEM 38», «QUINCENA N° 19» → «QNA 19» (the client's sheet names).</summary>
    private static string SheetName(string label)
    {
        var number = label.Split(' ').Last();
        return (label.StartsWith("SEMANA", StringComparison.Ordinal) ? "SEM " : "QNA ") + number;
    }

    // ---- Recibos (MigraDoc): two to a letter page ----

    private static readonly Color Shade = Color.FromRgb(0xE8, 0xEA, 0xF0);
    private static readonly Color Rule = Color.FromRgb(0xD5, 0xD5, 0xD5);
    private static readonly Color Muted = Color.FromRgb(0x61, 0x61, 0x61);
    private static readonly System.Globalization.CultureInfo Mx = System.Globalization.CultureInfo.GetCultureInfo("es-MX");

    private static string Money(decimal value) => (value < 0 ? "-" : "") + "$" + Math.Abs(value).ToString("#,##0.00", Mx);

    public byte[] Receipts(PrepayrollHeading heading, IReadOnlyList<Receipt> receipts)
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Lato";
        normal.Font.Size = 9;
        var section = document.AddSection();
        section.PageSetup = document.DefaultPageSetup.Clone();
        section.PageSetup.PageWidth = Unit.FromInch(8.5);
        section.PageSetup.PageHeight = Unit.FromInch(11);
        var margin = Unit.FromPoint(36);
        section.PageSetup.TopMargin = section.PageSetup.BottomMargin = margin;
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = margin;
        var width = Unit.FromInch(8.5).Point - 2 * margin.Point;

        for (var i = 0; i < receipts.Count; i++)
        {
            if (i > 0 && i % 2 == 0)
                section.AddPageBreak();
            else if (i % 2 == 1)
            {
                var cut = section.AddParagraph();
                cut.Format.SpaceBefore = Unit.FromPoint(18);
                cut.Format.SpaceAfter = Unit.FromPoint(18);
                cut.Format.Borders.Bottom.Width = 0.5;
                cut.Format.Borders.Bottom.Color = Rule;
            }
            Receipt(section, heading, receipts[i], width);
        }
        if (receipts.Count == 0)
            section.AddParagraph("El período no tiene líneas.");

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void Receipt(Section section, PrepayrollHeading heading, Receipt r, double width)
    {
        var title = section.AddParagraph($"Recibo de pago · {heading.PeriodLabel} · {heading.DateRange}");
        title.Format.Font.Size = 11;
        title.Format.Font.Bold = true;
        var who = section.AddParagraph($"{heading.ClientName} · {heading.GroupName}");
        who.Format.Font.Color = Muted;
        who.Format.SpaceAfter = Unit.FromPoint(6);
        var name = section.AddParagraph(r.FullName);
        name.Format.Font.Size = 11;
        name.Format.Font.Bold = true;
        section.AddParagraph($"{r.PositionName} · {r.Shift} · Días laborados: {r.DaysWorked.ToString("0.##", Mx)}").Format.SpaceAfter = Unit.FromPoint(6);

        var table = section.AddTable();
        table.AddColumn(Unit.FromPoint(width * 0.6));
        table.AddColumn(Unit.FromPoint(width * 0.15));
        table.AddColumn(Unit.FromPoint(width * 0.25));
        table.TopPadding = table.BottomPadding = Unit.FromPoint(2);
        var header = table.AddRow();
        header.Shading.Color = Shade;
        header.Format.Font.Bold = true;
        Cell(header.Cells[0], "Concepto", false);
        Cell(header.Cells[1], "Cantidad", true);
        Cell(header.Cells[2], "Importe", true);
        foreach (var item in r.Earnings.Concat(r.Deductions))
        {
            var row = table.AddRow();
            row.Borders.Bottom.Width = 0.5;
            row.Borders.Bottom.Color = Rule;
            Cell(row.Cells[0], item.Name, false);
            Cell(row.Cells[1], item.Quantity.ToString("0.##", Mx), true);
            Cell(row.Cells[2], Money(item.Amount), true);
        }
        var total = table.AddRow();
        total.Format.Font.Bold = true;
        total.Shading.Color = Shade;
        Cell(total.Cells[0], "TOTAL NETO A PAGAR", false);
        Cell(total.Cells[2], Money(r.TotalNet), true);

        var pay = section.AddParagraph(r.Payment.State switch
        {
            _ when r.Payment.Last4 is { } last4 => $"Depósito: {r.Payment.Bank} · terminación {last4}"
                + (r.Payment.ThirdPartyHolder is { } holder ? $" · a nombre de {holder}" : ""),
            PaymentState.CardRequested => "Depósito: tarjeta en trámite",
            PaymentState.NothingDue => "Sin depósito en este período",
            _ => "Depósito: sin cuenta registrada",
        });
        pay.Format.SpaceBefore = Unit.FromPoint(6);
        pay.Format.Font.Color = Muted;
        var sign = section.AddParagraph("Recibí la cantidad indicada: ______________________________");
        sign.Format.SpaceBefore = Unit.FromPoint(18);
    }

    private static void Cell(Cell cell, string text, bool right)
    {
        var p = cell.AddParagraph(text);
        p.Format.Alignment = right ? ParagraphAlignment.Right : ParagraphAlignment.Left;
    }
}
