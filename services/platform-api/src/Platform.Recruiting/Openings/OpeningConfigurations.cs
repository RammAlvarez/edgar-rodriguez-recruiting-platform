using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Core.Users;

namespace Platform.Recruiting.Openings;

internal sealed class JobOpeningConfiguration : IEntityTypeConfiguration<JobOpening>
{
    public void Configure(EntityTypeBuilder<JobOpening> b)
    {
        b.ToTable("job_openings", t =>
        {
            t.HasCheckConstraint("ck_job_openings_headcount", "headcount_requested > 0");
            t.HasCheckConstraint("ck_job_openings_salary", "monthly_salary IS NULL OR monthly_salary >= 0");
            t.HasCheckConstraint("ck_job_openings_years", "years_experience IS NULL OR years_experience >= 0");
            t.HasCheckConstraint("ck_job_openings_filled", "(status = 'filled') = (filled_at IS NOT NULL)");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(o => o.LegacyCode).IsUnique().HasFilter("legacy_code IS NOT NULL");
        b.Property(o => o.RequestedAt).HasDefaultValueSql("current_date");
        b.Property(o => o.MonthlySalary).HasPrecision(12, 2);
        b.Property(o => o.GenderRequirement).HasDefaultValue(Gender.Unspecified).HasSentinel(Gender.Unspecified);
        b.Property(o => o.Status).HasDefaultValue(OpeningStatus.Open).HasSentinel(OpeningStatus.Open);
        b.Property(o => o.VisibleToClient).HasDefaultValue(true).HasSentinel(true);
        b.HasIndex(o => new { o.ClientId, o.Status });
        b.HasIndex(o => o.LeadRecruiterId);
        b.HasOne<Client>().WithMany().HasForeignKey(o => o.ClientId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Zone>().WithMany().HasForeignKey(o => o.ZoneId).HasPrincipalKey(z => z.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EducationLevel>().WithMany().HasForeignKey(o => o.EducationLevelId).HasPrincipalKey(e => e.Uid)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DocumentTemplate>().WithMany().HasForeignKey(o => o.DocumentTemplateId).HasPrincipalKey(t => t.Uid)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(o => o.LeadRecruiterId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(o => o.Recruiters).WithOne().HasForeignKey(r => r.JobOpeningId).HasPrincipalKey(o => o.Uid)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JobOpeningRecruiterConfiguration : IEntityTypeConfiguration<JobOpeningRecruiter>
{
    public void Configure(EntityTypeBuilder<JobOpeningRecruiter> b)
    {
        b.ToTable("job_opening_recruiters");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(r => r.IsLead).HasDefaultValue(false).HasSentinel(false);
        b.HasIndex(r => new { r.JobOpeningId, r.UserId }).IsUnique();
        // PLAN D71: one líder per vacante, mirrored in job_openings.lead_recruiter_id.
        b.HasIndex(r => r.JobOpeningId).IsUnique().HasFilter("is_lead").HasDatabaseName("ux_job_opening_recruiters_lead");
        b.HasIndex(r => r.UserId);
        b.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class JobOpeningEventConfiguration : IEntityTypeConfiguration<JobOpeningEvent>
{
    public void Configure(EntityTypeBuilder<JobOpeningEvent> b)
    {
        b.ToTable("job_opening_events", t => t.HasCheckConstraint("ck_job_opening_events_reason",
            "kind <> 'status_changed' OR to_status NOT IN ('paused', 'cancelled') OR reason IS NOT NULL"));
        b.HasIdentityAndUid();
        b.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
        b.HasIndex(e => new { e.JobOpeningId, e.OccurredAt });
        b.HasOne<JobOpening>().WithMany().HasForeignKey(e => e.JobOpeningId).HasPrincipalKey(o => o.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.ActingForUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.RecruiterUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OpeningCommentConfiguration : IEntityTypeConfiguration<OpeningComment>
{
    public void Configure(EntityTypeBuilder<OpeningComment> b)
    {
        b.ToTable("opening_comments", t => t.HasCheckConstraint("ck_opening_comments_body", "length(body) BETWEEN 1 AND 2000"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(c => c.VisibleToClient).HasDefaultValue(true).HasSentinel(true);
        b.HasIndex(c => new { c.JobOpeningId, c.CreatedAt });
        b.HasOne<JobOpening>().WithMany().HasForeignKey(c => c.JobOpeningId).HasPrincipalKey(o => o.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.ActingForUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
