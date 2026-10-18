using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Clients;
using Platform.Core.Persistence;

namespace Platform.Payroll.Groups;

internal sealed class PayrollGroupConfiguration : IEntityTypeConfiguration<PayrollGroup>
{
    public void Configure(EntityTypeBuilder<PayrollGroup> b)
    {
        b.ToTable("payroll_groups", t =>
        {
            t.HasCheckConstraint("ck_payroll_groups_cutoff", "cutoff_weekday BETWEEN 0 AND 6");
            t.HasCheckConstraint("ck_payroll_groups_shift", "shift_hours > 0 AND shift_hours <= 24");
            t.HasCheckConstraint("ck_payroll_groups_divisor", "seventh_day_divisor BETWEEN 1 AND 7");
            t.HasCheckConstraint("ck_payroll_groups_premium", "sunday_premium_pct BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_payroll_groups_holiday", "holiday_worked_multiplier BETWEEN 1 AND 5");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(g => g.PeriodType).HasDefaultValue(PeriodType.Weekly).HasSentinel(PeriodType.Weekly);
        b.Property(g => g.ShiftHours).HasPrecision(4, 2);
        b.Property(g => g.SundayPremiumPct).HasPrecision(5, 4);
        b.Property(g => g.HolidayWorkedMultiplier).HasPrecision(4, 2);
        b.Property(g => g.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.HasIndex(g => new { g.ClientId, g.Name }).IsUnique();
        b.HasOne<Client>().WithMany().HasForeignKey(g => g.ClientId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.ToTable("positions");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(p => p.IsActive).HasDefaultValue(true).HasSentinel(true);
        // Unique by name whatever its capitals: the expression index ux_positions_group_name (the migration), which EF cannot model.
        b.HasIndex(p => p.PayrollGroupId);
        b.HasOne<PayrollGroup>().WithMany().HasForeignKey(p => p.PayrollGroupId).HasPrincipalKey(g => g.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PositionRateConfiguration : IEntityTypeConfiguration<PositionRate>
{
    public void Configure(EntityTypeBuilder<PositionRate> b)
    {
        b.ToTable("position_rates", t =>
        {
            t.HasCheckConstraint("ck_position_rates_amounts",
                "daily_salary_pocket >= 0 AND daily_salary_imss >= 0 AND hourly_rate >= 0 AND days_worked_bonus >= 0");
            t.HasCheckConstraint("ck_position_rates_validity", "valid_to IS NULL OR valid_to >= valid_from");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(r => r.DailySalaryPocket).HasPrecision(12, 2);
        b.Property(r => r.DailySalaryImss).HasPrecision(12, 2);
        b.Property(r => r.HourlyRate).HasPrecision(12, 4);
        b.Property(r => r.DaysWorkedBonus).HasPrecision(12, 2);
        b.HasIndex(r => new { r.PositionId, r.ValidFrom }).IsUnique();
        // One open (current) row per puesto.
        b.HasIndex(r => r.PositionId).IsUnique().HasFilter("valid_to IS NULL").HasDatabaseName("ux_position_rates_current");
        b.HasOne<Position>().WithMany().HasForeignKey(r => r.PositionId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PayConceptRowConfiguration : IEntityTypeConfiguration<PayConceptRow>
{
    public void Configure(EntityTypeBuilder<PayConceptRow> b)
    {
        b.ToTable("pay_concepts");
        b.HasIdentityAndUid();
        b.Property(c => c.DefaultRate).HasPrecision(12, 4);
        b.Property(c => c.DefaultAmount).HasPrecision(12, 2);
        b.HasIndex(c => c.Code).IsUnique();
    }
}

internal sealed class PayrollGroupConceptConfiguration : IEntityTypeConfiguration<PayrollGroupConcept>
{
    public void Configure(EntityTypeBuilder<PayrollGroupConcept> b)
    {
        b.ToTable("payroll_group_concepts");
        b.HasKey(c => new { c.PayrollGroupId, c.PayConceptId });
        b.Property(c => c.RateOverride).HasPrecision(12, 4);
        b.Property(c => c.AmountOverride).HasPrecision(12, 2);
        b.HasOne<PayrollGroup>().WithMany().HasForeignKey(c => c.PayrollGroupId).HasPrincipalKey(g => g.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<PayConceptRow>().WithMany().HasForeignKey(c => c.PayConceptId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
