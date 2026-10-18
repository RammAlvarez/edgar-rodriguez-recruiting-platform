using Platform.Payroll.Engine;

namespace Platform.Payroll.Prepayroll;

/// <summary>«Excel del período», «Resumen PDF», «Recibos».</summary>
public enum PrepayrollExport
{
    Excel,
    Summary,
    Receipts,
}

public sealed record PrepayrollExportRequest(PrepayrollExport Kind);

/// <summary>One concept of a receipt: the exact item cut to cents for printing, signed toward net pay.</summary>
public sealed record ReceiptItem(string Name, decimal Quantity, decimal Amount);

/// <summary>A worker's receipt for the period (what the line pays and where it is deposited, never the account number).</summary>
public sealed record Receipt(string FullName, string PositionName, string Shift, decimal DaysWorked, IReadOnlyList<ReceiptItem> Earnings,
    IReadOnlyList<ReceiptItem> Deductions, decimal TotalNet, LinePaymentDto Payment, string? Comments);

/// <summary>The heading every file carries: «ECOVIA · VALIDADORES 12 HRS», «SEMANA N° 38», «11 AL 17 DE SEPTIEMBRE 2026».</summary>
public sealed record PrepayrollHeading(string ClientName, string GroupName, string? ScheduleDescription, string PeriodLabel, string DateRange);

/// <summary>
/// Writes the period's files (SPEC §5.5 exports): the Excel in the client's SEM 38 layout (headers B5:W5, data from row 6, cells
/// cut to cents) and the receipts PDF. The summary PDF goes through the report renderer.
/// </summary>
public interface IPrepayrollFiles
{
    byte[] Excel(PrepayrollHeading heading, PrepayrollGridDto grid);

    byte[] Receipts(PrepayrollHeading heading, IReadOnlyList<Receipt> receipts);
}

public static class PrepayrollLabels
{
    private static readonly string[] Months =
        ["ENERO", "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO", "JULIO", "AGOSTO", "SEPTIEMBRE", "OCTUBRE", "NOVIEMBRE", "DICIEMBRE"];

    /// <summary>
    /// The title a column shows. The columns keep the client's workbook names (they are also the keys of the cells), but the partial-hours
    /// one names the group's own turno — «SOBRE 12 HORAS» is ECOVIA's; an 8-hour group reads «SOBRE 8 HORAS» (E2E run).
    /// </summary>
    public static string ColumnTitle(string column, decimal shiftHours) =>
        column.EndsWith("SOBRE 12 HORAS", StringComparison.Ordinal)
            ? column[..^"12 HORAS".Length] + $"{shiftHours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} HORAS"
            : column;

    /// <summary>«SEMANA N° 38» / «QUINCENA N° 19».</summary>
    public static string Period(bool weekly, int number) => $"{(weekly ? "SEMANA" : "QUINCENA")} N° {number}";

    /// <summary>«11 AL 17 DE SEPTIEMBRE 2026», «28 DE SEPTIEMBRE AL 4 DE OCTUBRE 2026», «29 DE DICIEMBRE 2025 AL 4 DE ENERO 2026».</summary>
    public static string Range(DateOnly start, DateOnly end) =>
        start.Year != end.Year
            ? $"{start.Day} DE {Months[start.Month - 1]} {start.Year} AL {end.Day} DE {Months[end.Month - 1]} {end.Year}"
            : start.Month != end.Month
                ? $"{start.Day} DE {Months[start.Month - 1]} AL {end.Day} DE {Months[end.Month - 1]} {end.Year}"
                : $"{start.Day} AL {end.Day} DE {Months[end.Month - 1]} {end.Year}";

    public static decimal Cents(decimal amount) => PrepayrollCalculator.TruncateToCents(amount);
}
