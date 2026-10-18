using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Abstractions;
using Platform.Core.Catalogs;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;

namespace Platform.Bank.Payments;

/// <summary>Postgres enum <c>batch_status</c> (DATA_MODEL §2), shared by payment and registration batches.</summary>
public enum BatchStatus
{
    Generated,
    UploadedToBank,
    Completed,
    Cancelled,
}

/// <summary>Postgres enum <c>payment_batch_kind</c>: the period's first file, or a later file of its pending lines.</summary>
public enum PaymentBatchKind
{
    PeriodPayment,
    PendingPayment,
    FrequentAccounts,
}

/// <summary>Postgres enum <c>payment_type</c>: Azteca's TIPO DE PAGO (1 SPEI, 2 TEF, 3 traspaso BAZ) — derived, never typed.</summary>
public enum PaymentType
{
    Spei,
    Tef,
    BazTransfer,
}

/// <summary>Postgres enum <c>manual_payment_method</c>: how a line was paid outside the bank file.</summary>
public enum ManualPaymentMethod
{
    Cash,
    Check,
    Other,
}

/// <summary>
/// A line paid outside the bank files (cash, cheque, another transfer) once its period is paid: whoever has no account the file
/// can use — typically someone dismissed before their card arrived — is still paid, and the payment is recorded with who did it
/// and when. The line's own <see cref="PrepayrollLine.PaymentStatus"/> becomes <c>Paid</c>; <see cref="PreviousStatus"/> is what
/// it was, so a wrong entry can be undone.
/// </summary>
public sealed class ManualPayment : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PrepayrollLineId { get; set; }
    public ManualPaymentMethod Method { get; set; }
    /// <summary>Recibo, folio of the cheque, tracking key…</summary>
    public string? Reference { get; set; }
    public DateOnly PaidOn { get; set; }
    public decimal Amount { get; set; }
    public PaymentRowStatus PreviousStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §6.1: the account every payment is charged to — MAXIMA REPARACION Y LIMPIEZA SA DE CV at Banco Azteca (SPEC D17,
/// D24). The number is encrypted like the employees' accounts and only its last four digits are shown.
/// </summary>
public sealed class PayingAccount : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public Guid BankId { get; set; }
    public required byte[] ChargeAccountNumberEnc { get; set; }
    public required string ChargeAccountLast4 { get; set; }
    public required string HolderName { get; set; }
    public required string HolderRfc { get; set; }
    public string DefaultChargeLegend { get; set; } = "PAGO";
    public string DefaultCreditLegend { get; set; } = "PAGO";
    public string Currency { get; set; } = "MXP";

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §6.3, one Banco Azteca «pagos masivos» file of a period, with its cuentas frecuentes companion when some account
/// is not registered at the bank yet. <see cref="TotalAmount"/> is what this file pays, not the period's total.
/// </summary>
public sealed class PaymentBatch : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PayingAccountId { get; set; }
    public PaymentBatchKind Kind { get; set; }
    public Guid? PayrollPeriodId { get; set; }
    public Guid GeneratedBy { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Generated;
    public int RowCount { get; set; }
    public decimal TotalAmount { get; set; }
    public required string StoragePath { get; set; }
    public string? FrequentAccountsStoragePath { get; set; }
    public int FrequentAccountsCount { get; set; }
    public required string LayoutVersion { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
    public Guid? UploadedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public List<PaymentBatchRow> Rows { get; set; } = [];
}

/// <summary>
/// One row of the Azteca file: a line's payable amount to the account it was snapshotted with. A rejected row is paid again by
/// a later pending-payments file (a new row; this one keeps the history).
/// </summary>
public sealed class PaymentBatchRow : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PaymentBatchId { get; set; }
    public int LineNo { get; set; }
    public Guid PrepayrollLineId { get; set; }
    public Guid PersonBankAccountId { get; set; }
    /// <summary>NOMBRE DEL BENEFICIARIO: the titular of the principal account.</summary>
    public required string DisplayName { get; set; }
    public required string ReceivingBankCode { get; set; }
    public BankAccountType AccountType { get; set; }
    public required byte[] AccountNumberEnc { get; set; }
    public required string AccountLast4 { get; set; }
    public PaymentType PaymentType { get; set; }
    public decimal Amount { get; set; }
    public required string ChargeLegend { get; set; }
    public required string CreditLegend { get; set; }
    /// <summary>The account went into this batch's cuentas frecuentes file; the upload marks it registered.</summary>
    public bool RegistersFrequentAccount { get; set; }
    public PaymentRowStatus Status { get; set; } = PaymentRowStatus.Pending;
    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

internal sealed class ManualPaymentConfiguration : IEntityTypeConfiguration<ManualPayment>
{
    public void Configure(EntityTypeBuilder<ManualPayment> b)
    {
        b.ToTable("manual_payments", t => t.HasCheckConstraint("ck_manual_payments_amount", "amount > 0 AND amount = trunc(amount, 2)"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.HasIndex(x => x.PrepayrollLineId).IsUnique();
        b.HasOne<PrepayrollLine>().WithMany().HasForeignKey(x => x.PrepayrollLineId).HasPrincipalKey(l => l.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PayingAccountConfiguration : IEntityTypeConfiguration<PayingAccount>
{
    public void Configure(EntityTypeBuilder<PayingAccount> b)
    {
        b.ToTable("paying_accounts");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(a => a.Name).HasMaxLength(80);
        b.Property(a => a.ChargeAccountLast4).HasMaxLength(4);
        b.Property(a => a.HolderName).HasMaxLength(40);
        b.Property(a => a.HolderRfc).HasMaxLength(13);
        b.Property(a => a.DefaultChargeLegend).HasMaxLength(30).HasDefaultValue("PAGO");
        b.Property(a => a.DefaultCreditLegend).HasMaxLength(30).HasDefaultValue("PAGO");
        b.Property(a => a.Currency).HasMaxLength(3).HasDefaultValue("MXP");
        b.HasOne<Platform.Core.Catalogs.Bank>().WithMany().HasForeignKey(a => a.BankId).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentBatchConfiguration : IEntityTypeConfiguration<PaymentBatch>
{
    public void Configure(EntityTypeBuilder<PaymentBatch> b)
    {
        b.ToTable("payment_batches", t => t.HasCheckConstraint("ck_payment_batches_rows", "row_count > 0 AND total_amount > 0"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(x => x.Status).HasDefaultValue(BatchStatus.Generated).HasSentinel(BatchStatus.Generated);
        b.Property(x => x.TotalAmount).HasPrecision(14, 2);
        b.HasIndex(x => x.PayrollPeriodId);
        b.HasMany(x => x.Rows).WithOne().HasForeignKey(r => r.PaymentBatchId).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayingAccount>().WithMany().HasForeignKey(x => x.PayingAccountId).HasPrincipalKey(a => a.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.GeneratedBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentBatchRowConfiguration : IEntityTypeConfiguration<PaymentBatchRow>
{
    public void Configure(EntityTypeBuilder<PaymentBatchRow> b)
    {
        b.ToTable("payment_batch_rows", t => t.HasCheckConstraint("ck_payment_batch_rows_amount", "amount > 0 AND amount = trunc(amount, 2)"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(r => r.DisplayName).HasMaxLength(40);
        b.Property(r => r.ReceivingBankCode).HasMaxLength(3);
        b.Property(r => r.AccountLast4).HasMaxLength(4);
        b.Property(r => r.Amount).HasPrecision(12, 2);
        b.Property(r => r.ChargeLegend).HasMaxLength(30);
        b.Property(r => r.CreditLegend).HasMaxLength(30);
        b.Property(r => r.Status).HasDefaultValue(PaymentRowStatus.Pending).HasSentinel(PaymentRowStatus.Pending);
        b.Property(r => r.RejectionReason).HasMaxLength(200);
        b.HasIndex(r => new { r.PaymentBatchId, r.LineNo }).IsUnique();
        b.HasIndex(r => r.PrepayrollLineId);
        b.HasOne<PrepayrollLine>().WithMany().HasForeignKey(r => r.PrepayrollLineId).HasPrincipalKey(l => l.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonBankAccount>().WithMany().HasForeignKey(r => r.PersonBankAccountId).HasPrincipalKey(a => a.Uid)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
