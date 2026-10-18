using Platform.Core.People;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;
using Platform.Recruiting.Reports;

namespace Platform.Bank.Payments;

/// <summary>
/// Where a line stands for payment (SPEC §5.5 «Estado de pago»). Before the period is paid: listo, sin cuenta (tarjeta en
/// trámite or not), or nothing to pay; after: the state its last file left it in.
/// </summary>
public enum LinePaymentState
{
    Ready,
    CardRequested,
    MissingAccount,
    InvalidAccount,
    NothingDue,
    Pending,
    Paid,
    Rejected,
    PaidManually,
}

/// <summary>
/// A line in the payment panel: the amount and the account it goes to (bank and last four; never the number).
/// <see cref="InNextFile"/> = the file that can be generated now would pay it; <see cref="CanPayManually"/> = it can be recorded as
/// paid outside the files (the period is paid and the line is still owed).
/// </summary>
public sealed record PaymentLineDto(Guid LineId, Guid EmployeeId, string FullName, decimal Amount, LinePaymentState State, string? Bank,
    string? Last4, BankAccountType? AccountType, string? ThirdPartyHolder, bool NeedsFrequentAccount, bool InNextFile,
    bool CanPayManually, ManualPaymentDto? Manual);

/// <summary>How, when and with what reference a line was paid outside the bank files.</summary>
public sealed record ManualPaymentDto(ManualPaymentMethod Method, string? Reference, DateOnly PaidOn);

/// <summary>«Pagado fuera de archivo»: cash, cheque or another transfer, on the date it was paid.</summary>
public sealed record ManualPaymentRequest(ManualPaymentMethod Method, string? Reference, DateOnly PaidOn);

public sealed record PaymentStateTotalDto(LinePaymentState State, int Lines, decimal Amount);

/// <summary>The account every file charges, by its last four digits.</summary>
public sealed record PayingAccountDto(Guid Id, string Name, string Last4, string HolderName, string HolderRfc, string ChargeLegend,
    string CreditLegend, string Currency);

public sealed record PaymentBatchRowDto(Guid Id, int LineNo, Guid LineId, string FullName, string DisplayName, string Bank, string Last4,
    BankAccountType AccountType, PaymentType PaymentType, decimal Amount, PaymentRowStatus Status, string? RejectionReason,
    bool RegistersFrequentAccount);

/// <summary>
/// One payment file. <see cref="TotalAmount"/> is what this file pays («en este archivo»); the period's one total is the
/// grid's TOTAL NETO A PAGAR (D76).
/// </summary>
public sealed record PaymentBatchDto(Guid Id, PaymentBatchKind Kind, BatchStatus Status, DateTimeOffset GeneratedAt, string GeneratedBy,
    DateTimeOffset? UploadedAt, string? UploadedBy, int RowCount, decimal TotalAmount, int FrequentAccountsCount,
    IReadOnlyList<PaymentBatchRowDto> Rows);

/// <summary>
/// «Pagar» (SPEC §5.5): the period's lines by payment state, the cuenta de cargo, the files made so far, and what can be
/// generated now — the period's file while in Revisión, a pending-payments file once paid. <see cref="TotalNet"/> is the
/// period's one total.
/// </summary>
public sealed record PeriodPaymentsDto(Guid PeriodId, PeriodStatus Status, decimal TotalNet, DateTimeOffset? PaidAt, DateOnly? DepositDate,
    PayingAccountDto? PayingAccount, IReadOnlyList<PaymentLineDto> Lines, IReadOnlyList<PaymentStateTotalDto> Totals,
    int Payable, decimal PayableAmount, int FrequentAccounts, bool CanGenerate, string? CannotGenerate,
    IReadOnlyList<PaymentBatchDto> Batches);

/// <summary>A generated payment file with its download links (the cuentas frecuentes one only when it was needed).</summary>
public sealed record GeneratedPaymentFilesDto(PaymentBatchDto Batch, ReportExportDto Payments, ReportExportDto? FrequentAccounts,
    IReadOnlyList<string> Skipped);

public enum PaymentFile
{
    Payments,
    FrequentAccounts,
}

public sealed record PaymentDownloadRequest(PaymentFile File);

public sealed record RejectPaymentRowRequest(string Reason);

public sealed record DepositDateRequest(DateOnly? Date);

/// <summary>
/// «Cuenta de cargo»: the paying account's data. <see cref="ChargeAccountNumber"/> is typed only to set or replace it; left
/// empty on an edit, the stored number stays.
/// </summary>
public sealed record PayingAccountRequest(string Name, string? ChargeAccountNumber, string HolderName, string HolderRfc, string? ChargeLegend,
    string? CreditLegend);
