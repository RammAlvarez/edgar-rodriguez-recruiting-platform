using Platform.Bank.Payments;
using Platform.Core.Catalogs;
using Platform.Payroll.Staff;
using Platform.Recruiting.Reports;

namespace Platform.Bank.Registrations;

/// <summary>A broken bank rule on a person's row: the column as the bank names it and the message.</summary>
public sealed record RegistrationIssueDto(string Field, string Message);

/// <summary>
/// A queued request as the alta file would write it. <see cref="EmployeeNumber"/> is null when the person gets a new number
/// from the bank's sequence on generation.
/// </summary>
public sealed record RegistrationPreviewRowDto(Guid RequestId, Guid EmployeeId, string FullName, string ClientName, string GroupName,
    string? EmployeeNumber, IReadOnlyList<RegistrationIssueDto> Issues);

/// <summary>
/// The preview of an alta file (SPEC §5.7): every selected request with the bank's issues, the file-level issues, and the
/// people whose request is moot because they have a principal account now (generation cancels it).
/// </summary>
public sealed record RegistrationPreviewDto(RegistrationBank Bank, string? CardType, string? BranchToSend,
    IReadOnlyList<RegistrationPreviewRowDto> Rows, IReadOnlyList<string> FileIssues, IReadOnlyList<string> Moot, bool CanGenerate);

/// <summary>
/// «Generar alta»: the queued requests of one bank (all of them when <see cref="RequestIds"/> is empty). Banorte batches are
/// NOMINATIVA and need the 5-digit sucursal the cards are sent to (PLAN D85).
/// </summary>
public sealed record RegistrationBatchRequest(RegistrationBank Bank, IReadOnlyList<Guid>? RequestIds, string? BranchToSend);

public sealed record RegistrationBatchRowDto(Guid RequestId, Guid EmployeeId, string FullName, string EmployeeNumber, CardRequestStatus Status);

public sealed record RegistrationBatchDto(Guid Id, RegistrationBank Bank, BatchStatus Status, DateTimeOffset GeneratedAt, string GeneratedBy,
    DateTimeOffset? UploadedAt, string? CardType, string? BranchToSend, IReadOnlyList<RegistrationBatchRowDto> Rows);

public sealed record GeneratedRegistrationDto(RegistrationBatchDto Batch, ReportExportDto File);

/// <summary>«Tarjeta recibida»: the 16-digit number of the card the bank sent; it becomes the person's principal account.</summary>
public sealed record ReceiveCardRequest(string CardNumber);
