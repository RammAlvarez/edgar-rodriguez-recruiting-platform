using Platform.Core.People;
using Platform.Payroll.Engine;
using Platform.Payroll.Periods;
using Platform.Payroll.Staff;

namespace Platform.Payroll.Prepayroll;

public sealed record PayrollPeriodDto(Guid Id, Guid GroupId, string GroupName, Guid ClientId, string ClientName, int Number, int Year,
    DateOnly StartDate, DateOnly EndDate, PeriodStatus Status, DateTimeOffset? ReviewStartedAt, int Lines, decimal TotalNet);

/// <summary>«Crear período»: the period of the group that contains <see cref="Date"/> (idempotent).</summary>
public sealed record CreatePeriodRequest(DateOnly Date);

/// <summary>Whether the pay can be deposited: a current principal account, a card on its way, or nothing.</summary>
public enum PaymentState
{
    Ready,
    CardRequested,
    MissingAccount,
}

/// <summary>
/// The payment columns next to the Excel ones: bank, last four, type, and the holder only when the account is a third
/// party's. Never the number.
/// </summary>
public sealed record LinePaymentDto(PaymentState State, string? Bank, string? Last4, BankAccountType? AccountType, string? ThirdPartyHolder);

/// <summary>
/// One row of the grid in the client's layout (B–W of SEM 38). <see cref="Amounts"/> follows <see cref="PrepayrollGridDto.Columns"/>:
/// each cell is Σ of the column's exact items, signed toward net pay (DESCUENTO is negative) and truncated to cents for display;
/// null = nothing in it.
/// </summary>
public sealed record PrepayrollRowDto(Guid LineId, Guid EmployeeId, string FullName, DateOnly HiredAt, string PositionName, string Shift,
    decimal DaysWorked, decimal HourlyRate, decimal HolidaysWorked, decimal DailySalaryImss, decimal DailySalaryPocket,
    IReadOnlyList<decimal?> Amounts, decimal TotalNet, decimal TotalNetExact, string? Comments, bool Adjusted, LinePaymentDto Payment);

/// <summary>
/// The period's prenómina (SPEC §5.5). <see cref="Footers"/> = Σ of the cells shown in each column (D76: for display, they
/// are not what is paid); <see cref="TotalNet"/> = Σ TOTAL NETO A PAGAR, the amount paid; <see cref="TotalNetExact"/> = Σ of
/// the exact nets (D41).
/// </summary>
public sealed record PrepayrollGridDto(PayrollPeriodDto Period, string? ScheduleDescription, bool Editable, IReadOnlyList<string> Columns,
    IReadOnlyList<PrepayrollRowDto> Rows, IReadOnlyList<decimal> Footers, decimal TotalNet, decimal TotalNetExact);

public sealed record LineItemDto(string Code, string Name, string Column, ConceptKind Kind, decimal Quantity, decimal UnitValue,
    decimal Amount, ItemSource Source, Guid? IncidentId, string? Reason);

public sealed record LineDayDto(DateOnly Date, AttendanceStatus Status, decimal? HoursWorked, decimal OvertimeHours, bool IsAutoAbsent,
    bool IsHoliday);

/// <summary>«Desglose» of a row: every exact item (6 decimals), the days behind it and its incidencias.</summary>
public sealed record PrepayrollLineDetailDto(Guid LineId, Guid PeriodId, PeriodStatus PeriodStatus, Guid EmployeeId, string FullName,
    string PositionName, string Shift, decimal DaysWorked, int HolidaysEntitled, decimal HolidaysWorked, decimal PartialHours,
    decimal OvertimeHours, bool WorkedSunday, decimal DailySalaryPocket, decimal DailySalaryImss, decimal HourlyRate,
    IReadOnlyList<LineItemDto> Items, IReadOnlyList<LineDayDto> Days, IReadOnlyList<IncidentDto> Incidents, decimal GrossEarnings,
    decimal TotalDeductions, decimal TotalNetExact, decimal TotalNet, string? Comments, DateTimeOffset ComputedAt);

public sealed record LineCommentsRequest(string? Comments);

/// <summary>
/// «Editar celda»: the new value of a column of the row, signed as shown (a DESCUENTO is negative). Saved as an ajuste manual
/// of the difference, with its reason (D63); the column then shows <see cref="Amount"/>.
/// </summary>
public sealed record AdjustLineRequest(string Column, decimal Amount, string Reason);

public sealed record IncidentDto(Guid Id, Guid EmployeeId, string FullName, Guid PeriodId, int PeriodNumber, Guid? OriginPeriodId,
    int? OriginPeriodNumber, string ConceptCode, string ConceptName, string? AdjustedColumn, DateOnly Date, decimal Quantity, decimal Amount,
    string? Reason, bool DuringReview, string ReportedBy, DateTimeOffset CreatedAt, bool Editable);

/// <summary>
/// An incidencia: hour concepts (horas extra, dobles, triples) take <see cref="Quantity"/> hours; retroactivo, descuento and
/// abono a préstamo take <see cref="Amount"/> (positive; the concept gives the sign).
/// </summary>
public sealed record IncidentRequest(Guid EmployeeId, string ConceptCode, DateOnly Date, decimal? Quantity, decimal? Amount, string? Reason);
