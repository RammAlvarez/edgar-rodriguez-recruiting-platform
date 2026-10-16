using System.Text;
using System.Text.RegularExpressions;
using Platform.Core.Reports;
using Platform.Infrastructure.Reports;

namespace Platform.Tests.Reports;

/// <summary>The PDF writer (MigraDoc, PLAN D81) on its own: no system fonts needed, accents and a second page included.</summary>
public sealed class ReportRendererTests
{
    private static ReportDocument Long(int rows) => new("Rendimiento por reclutador", "Todos los clientes · 01/01/2026 – 16/10/2026",
    [
        new ReportTable(null, ["Vacante", "Presentados", "% conversión"],
            Enumerable.Range(1, rows).Select(i => new ReportRow(
                [ReportCell.Text($"PEÑA NIETO – ALBAÑIL {i}"), ReportCell.Count(i), ReportCell.Percent(0.66m)], Emphasis: i == 1)).ToList()),
    ]);

    [Fact]
    public void The_pdf_embeds_lato_and_breaks_long_tables_into_pages()
    {
        var bytes = new ReportRenderer().Render(Long(120), ReportFormat.Pdf);

        Assert.True(bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        var raw = Encoding.Latin1.GetString(bytes);
        Assert.Contains("+Lato#20Regular", raw); // embedded subsets of the two faces
        Assert.Contains("+Lato,Bold", raw);
        Assert.Contains("/MediaBox[0 0 792 612]", raw); // Letter landscape
        Assert.True(Regex.Count(raw, @"/Type/Page[^s]") > 1, "a 120-row table needs more than one page");
    }

    [Fact]
    public void PDFsharp_stays_on_its_last_MIT_release()
    {
        // 6.5.0 and later are AGPL-3.0 (PLAN D81): upgrading is a licensing decision, not a dependency bump.
        var version = typeof(PdfSharp.Pdf.PdfDocument).Assembly.GetName().Version!;
        Assert.Equal((6, 2), (version.Major, version.Minor));
    }
}
