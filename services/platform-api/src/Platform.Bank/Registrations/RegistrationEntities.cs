using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Bank.Payments;
using Platform.Core.Abstractions;
using Platform.Core.Catalogs;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Payroll.Staff;

namespace Platform.Bank.Registrations;

/// <summary>
/// DATA_MODEL §6.4, one Banorte «Alta Masiva NH» or Banregio alta file made from the card-request queue (SPEC §5.7). Banorte
/// batches are NOMINATIVA with the sucursal the cards are sent to, and carry no card numbers: they are typed when each card
/// arrives (PLAN D85).
/// </summary>
public sealed class RegistrationBatch : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public RegistrationBank Bank { get; set; }
    public Guid GeneratedBy { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Generated;
    public int RowCount { get; set; }
    public required string StoragePath { get; set; }
    public required string LayoutVersion { get; set; }
    // Banorte's values fixed for the batch.
    public string? OccupationCode { get; set; }
    public string? ProductCode { get; set; }
    public string? CardType { get; set; }
    public decimal? GrossMonthlySalary { get; set; }
    public string? BranchToSend { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
    public Guid? UploadedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public List<RegistrationBatchRow> Rows { get; set; } = [];
}

/// <summary>One employee of an alta file, with the bank employee number written for them.</summary>
public sealed class RegistrationBatchRow : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid RegistrationBatchId { get; set; }
    public int LineNo { get; set; }
    public Guid CardRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public required string EmployeeNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// DATA_MODEL §5.5: an employee's number at a bank («2000000020» at Banorte, «3000000001» at Banregio), taken from the bank's
/// sequence the first time they go into an alta file and reused after.
/// </summary>
public sealed class EmployeeBankNumber
{
    public Guid EmployeeId { get; set; }
    public RegistrationBank Bank { get; set; }
    public required string EmployeeNumber { get; set; }
}

internal sealed class RegistrationBatchConfiguration : IEntityTypeConfiguration<RegistrationBatch>
{
    public void Configure(EntityTypeBuilder<RegistrationBatch> b)
    {
        b.ToTable("registration_batches", t => t.HasCheckConstraint("ck_registration_batches_rows", "row_count > 0"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(x => x.Status).HasDefaultValue(BatchStatus.Generated).HasSentinel(BatchStatus.Generated);
        b.Property(x => x.GrossMonthlySalary).HasPrecision(12, 2);
        b.Property(x => x.BranchToSend).HasMaxLength(5);
        b.HasMany(x => x.Rows).WithOne().HasForeignKey(r => r.RegistrationBatchId).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.GeneratedBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RegistrationBatchRowConfiguration : IEntityTypeConfiguration<RegistrationBatchRow>
{
    public void Configure(EntityTypeBuilder<RegistrationBatchRow> b)
    {
        b.ToTable("registration_batch_rows");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(r => r.EmployeeNumber).HasMaxLength(10);
        b.HasIndex(r => new { r.RegistrationBatchId, r.LineNo }).IsUnique();
        b.HasIndex(r => r.CardRequestId).IsUnique();
        b.HasOne<CardRequest>().WithMany().HasForeignKey(r => r.CardRequestId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(r => r.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EmployeeBankNumberConfiguration : IEntityTypeConfiguration<EmployeeBankNumber>
{
    public void Configure(EntityTypeBuilder<EmployeeBankNumber> b)
    {
        b.ToTable("employee_bank_numbers");
        b.HasKey(x => new { x.EmployeeId, x.Bank });
        b.Property(x => x.EmployeeNumber).HasMaxLength(10);
        b.HasIndex(x => new { x.Bank, x.EmployeeNumber }).IsUnique();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
