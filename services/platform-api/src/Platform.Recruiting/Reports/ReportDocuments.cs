using System.Globalization;
using Platform.Core.Abstractions;
using Platform.Core.People;
using Platform.Core.Reports;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Reports;

/// <summary>
/// Each report as an export (<see cref="ReportDocument"/>), with the columns of the screen (prototype) in the same order. The
/// labels repeat the admin app's <c>recruiting-labels.ts</c>.
/// </summary>
public static class ReportDocuments
{
    public static string Title(RecruitingReport report) => report switch
    {
        RecruitingReport.RecruiterPerformance => "Rendimiento por reclutador",
        RecruitingReport.Summary => "Resumen de reclutamiento",
        RecruitingReport.OpenOpenings => "Vacantes abiertas y envejecimiento",
        RecruitingReport.StageFunnel => "Embudo por etapa",
        RecruitingReport.Sources => "Fuentes de reclutamiento",
        _ => "Vacantes por cliente",
    };

    public static string Stage(CandidateStage stage) => stage switch
    {
        CandidateStage.New => "Nuevo",
        CandidateStage.Contacted => "Contactado",
        CandidateStage.Interview => "Entrevista",
        CandidateStage.Suitable => "Apto",
        CandidateStage.NotSuitable => "No apto",
        CandidateStage.Hired => "Contratado",
        _ => "Descartado",
    };

    public static string Source(CandidateSource? source) => source switch
    {
        CandidateSource.Facebook => "Facebook",
        CandidateSource.Linkedin => "LinkedIn",
        CandidateSource.Whatsapp => "WhatsApp",
        CandidateSource.Referral => "Referido",
        CandidateSource.JobBoard => "Bolsa de trabajo",
        CandidateSource.Other => "Otro",
        _ => "Sin fuente",
    };

    public static string Priority(OpeningPriority priority) => priority switch
    {
        OpeningPriority.High => "Alta",
        OpeningPriority.Medium => "Media",
        _ => "Baja",
    };

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>«16/10/2026 13:05» in America/Monterrey, as the screen's timestamp pipe.</summary>
    public static string Timestamp(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(ClockExtensions.DefaultTimeZone))
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>«HEB · Mónica Soto · 01/01/2026 – 16/10/2026» (report 3: «… · al 16/10/2026»).</summary>
    public static string FiltersLine(ReportFiltersDto f, DateOnly? asOf = null) => string.Join(" · ",
        f.ClientName ?? "Todos los clientes",
        f.RecruiterName ?? "Todos los reclutadores",
        asOf is { } a ? $"Situación al {Date(a)}" : $"{Date(f.From)} – {Date(f.To)}");

    private static ReportCell T(string? text) => ReportCell.Text(text);

    private static ReportCell N(int? value) => ReportCell.Count(value);

    private static ReportCell P(decimal? fraction) => ReportCell.Percent(fraction);

    private static ReportCell A(decimal? value) => ReportCell.Average(value);

    public static ReportDocument RecruiterPerformance(RecruiterPerformanceReportDto r)
    {
        var rows = new List<ReportRow>();
        foreach (var g in r.Recruiters)
        {
            rows.Add(new ReportRow([T(g.RecruiterName), T(g.Assigned == 1 ? "1 vacante asignada" : $"{g.Assigned} vacantes asignadas"),
                T(g.Filled == 1 ? "1 cubierta" : $"{g.Filled} cubiertas"),
                T($"{ReportCell.Percent(g.Effectiveness).Display} efectividad"), T($"{ReportCell.Average(g.AvgDaysToFill).Display} días promedio"),
                ReportCell.Blank, ReportCell.Blank, ReportCell.Blank, ReportCell.Blank], Emphasis: true));
            rows.AddRange(g.Openings.Select(o => new ReportRow([T(o.OpeningName), T(o.ClientName), T(o.PositionName),
                T(Date(o.AssignedAt) + (o.IsLead ? " (líder)" : "")), N(o.Presented), N(o.Suitable), N(o.Hired), P(o.Conversion), N(o.DaysToFill)])));
        }
        return new ReportDocument(Title(RecruitingReport.RecruiterPerformance), FiltersLine(r.Filters),
            [new ReportTable(null, ["Vacante", "Cliente", "Puesto", "Fecha de asignación", "Presentados", "Aptos", "Contratados",
                "% conversión", "Días para cubrir"], rows)]);
    }

    private static IReadOnlyList<(string Label, Func<SummaryMetricsDto, ReportCell> Cell)> SummaryIndicators { get; } =
    [
        ("Vacantes registradas", m => N(m.Openings)),
        ("Abiertas", m => N(m.Open)),
        ("En proceso", m => N(m.InProgress)),
        ("Cubiertas", m => N(m.Filled)),
        ("Canceladas", m => N(m.Cancelled)),
        ("Pausadas", m => N(m.Paused)),
        ("Candidatos capturados", m => N(m.Candidates)),
        ("Aptos", m => N(m.Suitable)),
        ("Contratados", m => N(m.Hired)),
        ("Conversión", m => P(m.Conversion)),
        ("Días promedio de cobertura", m => A(m.AvgDaysToFill)),
    ];

    public static ReportDocument Summary(SummaryReportDto r)
    {
        var indicators = SummaryIndicators.Select(i => new ReportRow([T(i.Label), i.Cell(r.Current), i.Cell(r.Previous)])).ToList();
        var clients = r.Clients.Select(c => new ReportRow([T(c.ClientName), .. SummaryIndicators.Select(i => i.Cell(c.Metrics))])).ToList();
        return new ReportDocument(Title(RecruitingReport.Summary), FiltersLine(r.Filters),
        [
            new ReportTable("Indicadores", ["Indicador", "Rango", $"Rango anterior ({Date(r.PreviousFrom)} – {Date(r.PreviousTo)})"], indicators),
            new ReportTable("Por cliente", ["Cliente", "Vacantes", "Abiertas", "En proceso", "Cubiertas", "Canceladas", "Pausadas",
                "Candidatos", "Aptos", "Contratados", "Conversión", "Días cobertura"], clients),
        ]);
    }

    public static ReportDocument OpenOpenings(OpenOpeningsReportDto r) =>
        new(Title(RecruitingReport.OpenOpenings), FiltersLine(r.Filters, r.AsOf),
        [
            new ReportTable(null, ["Vacante", "Cliente", "Puesto", "Prioridad", "Solicitadas", "Contratadas", "Días abierta", "Nuevo",
                    "Contactado", "Entrevista", "Apto", "Último movimiento", "Reclutador", "Alerta"],
                r.Openings.Select(o => new ReportRow([T(o.OpeningName), T(o.ClientName), T(o.PositionName), T(Priority(o.Priority)),
                    N(o.Requested), N(o.Hired), N(o.DaysOpen), N(o.New), N(o.Contacted), N(o.Interview), N(o.Suitable),
                    T(Timestamp(o.LastActivityAt)), T(o.LeadRecruiterName ?? "Sin líder"),
                    T(o.Stale ? $"Sin actividad {o.IdleDays} días" : null)])).ToList()),
        ]);

    public static ReportDocument StageFunnel(StageFunnelReportDto r) =>
        new(Title(RecruitingReport.StageFunnel), FiltersLine(r.Filters),
        [
            new ReportTable(null, ["Etapa", "Entraron", "Pasaron a la siguiente", "Perdidos", "% que avanza", "Días promedio en etapa"],
                r.Stages.Select(s => new ReportRow([T(Stage(s.Stage)), N(s.Entered), N(s.Advanced), N(s.Lost), P(s.AdvanceRate),
                    A(s.AvgDaysInStage)])).ToList()),
            new ReportTable("Motivos de pérdida", ["Etapa / motivo", "Candidatos"],
                r.Losses.SelectMany(l => new[] { new ReportRow([T(Stage(l.Outcome)), N(l.Count)], Emphasis: true) }
                    .Concat(l.Reasons.Select(m => new ReportRow([T("   " + m.Reason), N(m.Count)])))).ToList()),
        ]);

    public static ReportDocument Sources(SourcesReportDto r) =>
        new(Title(RecruitingReport.Sources), FiltersLine(r.Filters),
        [
            new ReportTable(null, ["Fuente", "Candidatos", "Aptos", "Contratados", "% conversión", "Días promedio a contratación"],
                r.Sources.Select(s => new ReportRow([T(Source(s.Source)), N(s.Candidates), N(s.Suitable), N(s.Hired), P(s.Conversion),
                    A(s.AvgDaysToHire)])).ToList()),
        ]);

    public static ReportDocument ClientOpenings(ClientOpeningsReportDto r)
    {
        static ReportRow Row(ClientOpeningsRowDto c, bool total = false) => new([T(c.ClientName), N(c.Openings), N(c.HeadcountRequested),
            N(c.Hired), P(c.FillRate), N(c.Cancelled), A(c.AvgDaysToFill), N(c.CandidatesPresented)], total);
        return new ReportDocument(Title(RecruitingReport.ClientOpenings), FiltersLine(r.Filters),
        [
            new ReportTable(null, ["Cliente", "Vacantes", "Headcount solicitado", "Contratados", "Fill rate", "Canceladas",
                "Días promedio de cobertura", "Candidatos presentados"], [.. r.Clients.Select(c => Row(c)), Row(r.Total, true)]),
        ]);
    }
}
