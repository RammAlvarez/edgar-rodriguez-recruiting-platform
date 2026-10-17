using Platform.Core.Reports;
using Platform.Payroll.Periods;

namespace Platform.Bank.Reports;

/// <summary>The six reports of SPEC §5.9, in the selector's order.</summary>
public enum PayrollReport
{
    Cost,
    Payments,
    Incidents,
    Attendance,
    Turnover,
    Accounts,
}

/// <summary>
/// The filters as applied (defaults filled in): Cliente (<c>null</c> = Todos, within the grant's clients) and either one period
/// (Semana) or a range of dates — for Rotación a range of months. <see cref="Description"/> is the line printed under the title.
/// </summary>
public sealed record PayrollReportFiltersDto(Guid? ClientId, string? ClientName, Guid? PeriodId, string? PeriodLabel, DateOnly? From,
    DateOnly? To, string Description);

/// <summary>A row; <see cref="PeriodId"/> lets the screen open the period's prenómina (report 1's drill-down).</summary>
public sealed record PayrollReportRowDto(IReadOnlyList<ReportCell> Cells, bool Emphasis = false, Guid? PeriodId = null);

public sealed record PayrollReportTableDto(string? Heading, IReadOnlyList<string> Columns, IReadOnlyList<PayrollReportRowDto> Rows);

/// <summary>
/// A report exactly as shown and exported (PLAN D87): its tables of cells already formatted and cut the way D42 says, so the
/// screen, the Excel and the PDF agree. <see cref="Note"/> explains what a figure is (e.g. column sums are informative, D76).
/// </summary>
public sealed record PayrollReportDto(PayrollReport Report, string Title, PayrollReportFiltersDto Filters,
    IReadOnlyList<PayrollReportTableDto> Tables, string? Note);

public sealed record PayrollReportClientDto(Guid Id, string Name);

/// <summary>A period in the Semana picker: «ECOVIA · VALIDADORES 12 HRS · Semana 38 · 11–17 sep».</summary>
public sealed record PayrollReportPeriodDto(Guid Id, Guid ClientId, string Label, PeriodStatus Status, DateOnly StartDate, DateOnly EndDate);

/// <summary>The pickers: the grant's clients and the periods with lines, newest first.</summary>
public sealed record PayrollReportOptionsDto(IReadOnlyList<PayrollReportClientDto> Clients, IReadOnlyList<PayrollReportPeriodDto> Periods);

/// <summary>«Exportar Excel / PDF»: the same filters as the screen.</summary>
public sealed record PayrollReportExportRequest(PayrollReport Report, ReportFormat Format, Guid? ClientId, Guid? PeriodId, DateOnly? From,
    DateOnly? To);
