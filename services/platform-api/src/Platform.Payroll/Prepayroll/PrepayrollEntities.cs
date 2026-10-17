using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Abstractions;
using Platform.Core.Catalogs;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Staff;

namespace Platform.Payroll.Prepayroll;

/// <summary>
/// DATA_MODEL §5.9, an «incidencia»: what changes pay and is not attendance (horas extra dobles / triples, retroactivo,
/// descuento, abono a préstamo, ajuste manual). <see cref="PayrollPeriodId"/> is the period it is paid in; when the date falls
/// in a paid period it goes to the next open one and <see cref="OriginPeriodId"/> keeps where it belongs («corresponde a
/// Semana 38»).
/// </summary>
public sealed class Incident : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid EmployeeId { get; set; }
    public Guid PayrollPeriodId { get; set; }
    public Guid? OriginPeriodId { get; set; }
    public Guid PayConceptId { get; set; }
    /// <summary>For a <c>MANUAL_ADJUSTMENT</c>: the concept whose column it adjusts (D63).</summary>
    public Guid? AdjustedConceptId { get; set; }
    public DateOnly IncidentDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitValue { get; set; }
    /// <summary>Positive; the concept kind gives the sign. Only a manual adjustment is signed (change to net pay).</summary>
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    /// <summary>Captured while the period was in Revisión («cambio durante revisión», SPEC §5.5).</summary>
    public bool DuringReview { get; set; }
    public Guid ReportedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §5.11, one Excel row: an employee in a period, recomputed by the API in the same transaction as every source
/// write (attendance, incidents, status, rates — D38, D84). Derived: not audited (its sources are).
/// </summary>
public sealed class PrepayrollLine : IDerived
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PayrollPeriodId { get; set; }
    public Guid EmployeeId { get; set; }
    public required string PositionName { get; set; }
    public required string Shift { get; set; }
    public DateOnly HiredAt { get; set; }
    public decimal DaysWorked { get; set; }
    public decimal HolidaysWorked { get; set; }
    public int HolidaysEntitled { get; set; }
    public decimal PartialHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal OvertimeDoubleHours { get; set; }
    public decimal OvertimeTripleHours { get; set; }
    public bool WorkedSunday { get; set; }
    public decimal DailySalaryPocket { get; set; }
    public decimal DailySalaryImss { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNetExact { get; set; }
    /// <summary>TOTAL NETO A PAGAR = trunc(<see cref="TotalNetExact"/>, 2): the amount paid (D42).</summary>
    public decimal TotalNet { get; set; }
    /// <summary>COMENTARIOS: free text only, never bank data.</summary>
    public string? Comments { get; set; }
    public DateTimeOffset ComputedAt { get; set; }

    // The account the line is paid to, copied from the person's principal account when it goes into a payment file (S10);
    // null while the period is open (the grid shows the live account) and for a line never paid.
    public Guid? BankId { get; set; }
    public BankAccountType? AccountType { get; set; }
    public byte[]? AccountNumberEnc { get; set; }
    public string? AccountLast4 { get; set; }
    public string? HolderName { get; set; }
    /// <summary>Set when the period is paid (S10); null while open, and for a line with nothing to pay (net ≤ 0).</summary>
    public PaymentRowStatus? PaymentStatus { get; set; }

    public List<PrepayrollLineItem> Items { get; set; } = [];
}

/// <summary>
/// Postgres enum <c>payment_row_status</c> (DATA_MODEL §2): a line or a payment-file row. <c>Pending</c> = in a file not yet
/// uploaded; the others are the reasons a line is not in a file yet, or what the bank did with it.
/// </summary>
public enum PaymentRowStatus
{
    Ready,
    MissingAccount,
    InvalidAccount,
    Pending,
    Paid,
    Rejected,
}

/// <summary>DATA_MODEL §5.12: one column of the row, exact and stored with 6 decimals (D42).</summary>
public sealed class PrepayrollLineItem : IDerived
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PrepayrollLineId { get; set; }
    public Guid PayConceptId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitValue { get; set; }
    public decimal Amount { get; set; }
    public ItemSource Source { get; set; }
    public Guid? IncidentId { get; set; }
}

internal sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> b)
    {
        b.ToTable("incidents", t =>
        {
            t.HasCheckConstraint("ck_incidents_quantity", "quantity >= 0");
            t.HasCheckConstraint("ck_incidents_amount", "amount >= 0 OR adjusted_concept_id IS NOT NULL");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(i => i.Quantity).HasPrecision(8, 2);
        b.Property(i => i.UnitValue).HasPrecision(12, 4);
        b.Property(i => i.Amount).HasPrecision(12, 2);
        b.Property(i => i.DuringReview).HasDefaultValue(false).HasSentinel(false);
        b.HasIndex(i => new { i.PayrollPeriodId, i.EmployeeId });
        b.HasOne<Employee>().WithMany().HasForeignKey(i => i.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(i => i.PayrollPeriodId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(i => i.OriginPeriodId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayConceptRow>().WithMany().HasForeignKey(i => i.PayConceptId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayConceptRow>().WithMany().HasForeignKey(i => i.AdjustedConceptId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.ReportedBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PrepayrollLineConfiguration : IEntityTypeConfiguration<PrepayrollLine>
{
    public void Configure(EntityTypeBuilder<PrepayrollLine> b)
    {
        b.ToTable("prepayroll_lines", t =>
        {
            t.HasCheckConstraint("ck_prepayroll_lines_net", "total_net_exact = gross_earnings - total_deductions");
            t.HasCheckConstraint("ck_prepayroll_lines_payable", "total_net = trunc(total_net_exact, 2)");
        });
        b.HasIdentityAndUid();
        foreach (var name in new[] { nameof(PrepayrollLine.DaysWorked), nameof(PrepayrollLine.HolidaysWorked) })
            b.Property(name).HasPrecision(4, 2);
        foreach (var name in new[] { nameof(PrepayrollLine.PartialHours), nameof(PrepayrollLine.OvertimeHours),
                     nameof(PrepayrollLine.OvertimeDoubleHours), nameof(PrepayrollLine.OvertimeTripleHours) })
            b.Property(name).HasPrecision(6, 2);
        b.Property(l => l.DailySalaryPocket).HasPrecision(12, 2);
        b.Property(l => l.DailySalaryImss).HasPrecision(12, 2);
        b.Property(l => l.HourlyRate).HasPrecision(12, 4);
        foreach (var name in new[] { nameof(PrepayrollLine.GrossEarnings), nameof(PrepayrollLine.TotalDeductions), nameof(PrepayrollLine.TotalNetExact) })
            b.Property(name).HasPrecision(18, 6);
        b.Property(l => l.TotalNet).HasPrecision(12, 2);
        b.Property(l => l.AccountLast4).HasMaxLength(4);
        b.Property(l => l.HolderName).HasMaxLength(40);
        b.HasIndex(l => new { l.PayrollPeriodId, l.EmployeeId }).IsUnique();
        b.HasOne<Bank>().WithMany().HasForeignKey(l => l.BankId).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(l => l.Items).WithOne().HasForeignKey(i => i.PrepayrollLineId).HasPrincipalKey(l => l.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(l => l.PayrollPeriodId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(l => l.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PrepayrollLineItemConfiguration : IEntityTypeConfiguration<PrepayrollLineItem>
{
    public void Configure(EntityTypeBuilder<PrepayrollLineItem> b)
    {
        b.ToTable("prepayroll_line_items");
        b.HasIdentityAndUid();
        b.Property(i => i.Quantity).HasPrecision(12, 4);
        b.Property(i => i.UnitValue).HasPrecision(18, 6);
        b.Property(i => i.Amount).HasPrecision(18, 6);
        b.HasIndex(i => i.PrepayrollLineId);
        b.HasOne<PayConceptRow>().WithMany().HasForeignKey(i => i.PayConceptId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Incident>().WithMany().HasForeignKey(i => i.IncidentId).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
