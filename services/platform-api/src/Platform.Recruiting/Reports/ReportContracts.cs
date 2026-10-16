using Platform.Core.People;
using Platform.Core.Reports;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Reports;

/// <summary>The six reports of SPEC §4.7, in the selector's order.</summary>
public enum RecruitingReport
{
    RecruiterPerformance,
    Summary,
    OpenOpenings,
    StageFunnel,
    Sources,
    ClientOpenings,
}

/// <summary>
/// The filters as applied (defaults filled in): Cliente and Reclutador (<c>null</c> = Todos) and the range — <see cref="From"/>
/// … <see cref="To"/> inclusive, default *Año en curso* (1 January … today in America/Monterrey). Report 3 has no range.
/// </summary>
public sealed record ReportFiltersDto(Guid? ClientId, string? ClientName, Guid? RecruiterId, string? RecruiterName, DateOnly From,
    DateOnly To);

// Percentages are fractions truncated to whole points (0.66) and averages truncated to 1 decimal (D42), exactly as shown and exported.

/// <summary>1 · one vacante of a recruiter.</summary>
public sealed record RecruiterPerformanceRowDto(Guid OpeningId, string OpeningName, string ClientName, string PositionName, bool IsLead,
    DateOnly AssignedAt, OpeningStatus Status, int Presented, int Suitable, int Hired, decimal? Conversion, int? DaysToFill);

/// <summary>1 · a recruiter's total row (vacantes asignadas, cubiertas, % efectividad, días promedio) and their vacantes.</summary>
public sealed record RecruiterPerformanceGroupDto(Guid RecruiterId, string RecruiterName, int Assigned, int Filled, decimal? Effectiveness,
    decimal? AvgDaysToFill, IReadOnlyList<RecruiterPerformanceRowDto> Openings);

public sealed record RecruiterPerformanceReportDto(ReportFiltersDto Filters, IReadOnlyList<RecruiterPerformanceGroupDto> Recruiters);

/// <summary>2 · the indicators of one range (all clients, or one client).</summary>
public sealed record SummaryMetricsDto(int Openings, int Open, int InProgress, int Filled, int Cancelled, int Paused, int Candidates,
    int Suitable, int Hired, decimal? Conversion, decimal? AvgDaysToFill);

public sealed record ClientSummaryDto(Guid ClientId, string ClientName, SummaryMetricsDto Metrics);

/// <summary>2 · the range, the previous range of equal length (for the Δ) and the same indicators per client.</summary>
public sealed record SummaryReportDto(ReportFiltersDto Filters, SummaryMetricsDto Current, DateOnly PreviousFrom, DateOnly PreviousTo,
    SummaryMetricsDto Previous, IReadOnlyList<ClientSummaryDto> Clients);

/// <summary>3 · an open or in-process vacante; <see cref="Stale"/> = no movement for more than <c>StaleAfterDays</c>.</summary>
public sealed record OpenOpeningDto(Guid OpeningId, string OpeningName, string ClientName, string PositionName, OpeningPriority Priority,
    OpeningStatus Status, int Requested, int Hired, int DaysOpen, int New, int Contacted, int Interview, int Suitable,
    DateTimeOffset LastActivityAt, int IdleDays, bool Stale, Guid? LeadRecruiterId, string? LeadRecruiterName);

/// <summary>3 · a snapshot «al día de hoy», oldest first.</summary>
public sealed record OpenOpeningsReportDto(ReportFiltersDto Filters, DateOnly AsOf, int StaleAfterDays, IReadOnlyList<OpenOpeningDto> Openings);

/// <summary>
/// 4 · one stage of Nuevo → Contratado: candidates who entered it in the range, how many moved on to a later stage, how many
/// were lost from it (next move No apto / Descartado) and the average days they stayed. Contratado has no «pasaron».
/// </summary>
public sealed record FunnelStageDto(CandidateStage Stage, int Entered, int? Advanced, int Lost, decimal? AdvanceRate, decimal? AvgDaysInStage);

public sealed record FunnelReasonDto(string Reason, int Count);

/// <summary>4 · the moves to No apto or Descartado in the range, by motivo.</summary>
public sealed record FunnelLossDto(CandidateStage Outcome, int Count, IReadOnlyList<FunnelReasonDto> Reasons);

public sealed record StageFunnelReportDto(ReportFiltersDto Filters, IReadOnlyList<FunnelStageDto> Stages, IReadOnlyList<FunnelLossDto> Losses);

/// <summary>5 · one fuente (<c>null</c> = «Sin fuente») over the candidates captured in the range.</summary>
public sealed record SourceRowDto(CandidateSource? Source, int Candidates, int Suitable, int Hired, decimal? Conversion, decimal? AvgDaysToHire);

public sealed record SourcesReportDto(ReportFiltersDto Filters, IReadOnlyList<SourceRowDto> Sources);

/// <summary>6 · one client (or the total, <see cref="ClientId"/> <c>null</c>): fill rate = contratados ÷ headcount solicitado.</summary>
public sealed record ClientOpeningsRowDto(Guid? ClientId, string ClientName, int Openings, int HeadcountRequested, int Hired, decimal? FillRate,
    int Cancelled, decimal? AvgDaysToFill, int CandidatesPresented);

public sealed record ClientOpeningsReportDto(ReportFiltersDto Filters, IReadOnlyList<ClientOpeningsRowDto> Clients, ClientOpeningsRowDto Total);

/// <summary>«Exportar Excel / PDF»: the same filters as the screen.</summary>
public sealed record ReportExportRequest(RecruitingReport Report, ReportFormat Format, Guid? ClientId, Guid? RecruiterId, DateOnly? From,
    DateOnly? To);

/// <summary>A presigned download of the generated file (bucket <c>exports</c>, 90-day lifecycle).</summary>
public sealed record ReportExportDto(string Url, string FileName, DateTimeOffset ExpiresAt);
