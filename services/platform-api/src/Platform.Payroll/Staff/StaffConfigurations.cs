using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Payroll.Groups;
using Platform.Recruiting.Candidates;

namespace Platform.Payroll.Staff;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("employees", t =>
        {
            t.HasCheckConstraint("ck_employees_terminated", "terminated_at IS NULL OR terminated_at >= hired_at");
            t.HasCheckConstraint("ck_employees_overrides",
                "COALESCE(daily_salary_pocket_override, 0) >= 0 AND COALESCE(daily_salary_imss_override, 0) >= 0 "
                + "AND COALESCE(hourly_rate_override, 0) >= 0 AND COALESCE(days_worked_bonus_override, 0) >= 0");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(e => e.Status).HasDefaultValue(EmployeeStatus.Active).HasSentinel(EmployeeStatus.Active);
        b.Property(e => e.DailySalaryPocketOverride).HasPrecision(12, 2);
        b.Property(e => e.DailySalaryImssOverride).HasPrecision(12, 2);
        b.Property(e => e.HourlyRateOverride).HasPrecision(12, 4);
        b.Property(e => e.DaysWorkedBonusOverride).HasPrecision(12, 2);
        // One active employment per person and group (DATA_MODEL §5.4); a reingreso after a baja is a new row.
        b.HasIndex(e => new { e.PersonId, e.PayrollGroupId }).IsUnique().HasFilter("terminated_at IS NULL")
            .HasDatabaseName("ux_employees_active_person_group");
        b.HasIndex(e => e.PayrollGroupId);
        b.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollGroup>().WithMany().HasForeignKey(e => e.PayrollGroupId).HasPrincipalKey(g => g.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Position>().WithMany().HasForeignKey(e => e.PositionId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Candidate>().WithMany().HasForeignKey(e => e.CandidateId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EmployeeStatusPeriodConfiguration : IEntityTypeConfiguration<EmployeeStatusPeriod>
{
    public void Configure(EntityTypeBuilder<EmployeeStatusPeriod> b)
    {
        b.ToTable("employee_status_periods", t =>
        {
            t.HasCheckConstraint("ck_employee_status_periods_status", "status <> 'active'");
            // A baja has no end; every other period has one (estimated for an incapacidad).
            t.HasCheckConstraint("ck_employee_status_periods_end", "(status = 'terminated') = (end_date IS NULL)");
            t.HasCheckConstraint("ck_employee_status_periods_dates", "end_date IS NULL OR end_date >= start_date");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(p => p.EmployeeId);
        b.HasOne<Employee>().WithMany().HasForeignKey(p => p.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CardRequestConfiguration : IEntityTypeConfiguration<CardRequest>
{
    public void Configure(EntityTypeBuilder<CardRequest> b)
    {
        b.ToTable("card_requests");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(r => r.Status).HasDefaultValue(CardRequestStatus.Queued).HasSentinel(CardRequestStatus.Queued);
        // One open request per employee.
        b.HasIndex(r => r.EmployeeId).IsUnique().HasFilter("status IN ('queued', 'in_batch', 'uploaded')")
            .HasDatabaseName("ux_card_requests_open");
        b.HasIndex(r => r.Status);
        b.HasOne<Employee>().WithMany().HasForeignKey(r => r.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> b)
    {
        b.ToTable("work_schedules", t =>
        {
            t.HasCheckConstraint("ck_work_schedules_kind",
                "(one_off_date IS NULL AND cardinality(weekdays) > 0) OR (one_off_date IS NOT NULL AND cardinality(weekdays) = 0)");
            t.HasCheckConstraint("ck_work_schedules_weekdays", "weekdays <@ ARRAY[1,2,3,4,5,6,7]::smallint[]");
            t.HasCheckConstraint("ck_work_schedules_validity", "valid_to IS NULL OR valid_to >= valid_from");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(s => s.EmployeeId);
        b.HasIndex(s => s.ClientSiteId);
        b.HasOne<Employee>().WithMany().HasForeignKey(s => s.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClientSite>().WithMany().HasForeignKey(s => s.ClientSiteId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.ToTable("attendance_records", t =>
        {
            t.HasCheckConstraint("ck_attendance_records_hours", "hours_worked IS NULL OR hours_worked BETWEEN 0 AND 24");
            t.HasCheckConstraint("ck_attendance_records_overtime", "overtime_hours BETWEEN 0 AND 24");
        });
        b.HasIdentityAndUid();
        b.Property(a => a.HoursWorked).HasPrecision(4, 2);
        b.Property(a => a.OvertimeHours).HasPrecision(4, 2).HasDefaultValue(0m).HasSentinel(0m);
        b.Property(a => a.IsLocked).HasDefaultValue(false).HasSentinel(false);
        b.Property(a => a.IsAutoAbsent).HasDefaultValue(false).HasSentinel(false);
        b.Property(a => a.RecordedAt).HasDefaultValueSql("now()");
        b.HasIndex(a => new { a.EmployeeId, a.WorkDate }).IsUnique();
        b.HasIndex(a => a.WorkDate);
        b.HasOne<Employee>().WithMany().HasForeignKey(a => a.EmployeeId).HasPrincipalKey(e => e.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ClientSite>().WithMany().HasForeignKey(a => a.ClientSiteId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeStatusPeriod>().WithMany().HasForeignKey(a => a.StatusPeriodId).HasPrincipalKey(p => p.Uid)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
