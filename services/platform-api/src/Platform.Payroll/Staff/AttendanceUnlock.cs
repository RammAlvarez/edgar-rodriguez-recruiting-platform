using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Persistence;
using Platform.Core.Users;

namespace Platform.Payroll.Staff;

/// <summary>
/// DATA_MODEL §5.8, «a menos que yo le dé permiso»: one change to a locked day, with who, when, why and the values before and
/// after (<c>null</c> before = the day had no record). Append-only like the other log tables (D53).
/// </summary>
public sealed class AttendanceUnlock
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid AttendanceRecordId { get; set; }
    public Guid UnlockedBy { get; set; }
    public DateTimeOffset UnlockedAt { get; set; }
    public required string Reason { get; set; }
    public string? PreviousValue { get; set; }
    public required string NewValue { get; set; }
}

internal sealed class AttendanceUnlockConfiguration : IEntityTypeConfiguration<AttendanceUnlock>
{
    public void Configure(EntityTypeBuilder<AttendanceUnlock> b)
    {
        b.ToTable("attendance_unlocks");
        b.HasIdentityAndUid();
        b.Property(u => u.PreviousValue).HasColumnType("jsonb");
        b.Property(u => u.NewValue).HasColumnType("jsonb");
        b.HasIndex(u => u.AttendanceRecordId);
        b.HasOne<AttendanceRecord>().WithMany().HasForeignKey(u => u.AttendanceRecordId).HasPrincipalKey(a => a.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(u => u.UnlockedBy).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
