using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Candidates;

internal sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.ToTable("candidates", t =>
        {
            t.HasCheckConstraint("ck_candidates_reason",
                "stage NOT IN ('not_suitable', 'discarded') OR discard_reason IS NOT NULL");
            t.HasCheckConstraint("ck_candidates_hired", "is_hired = (stage = 'hired') AND (stage <> 'hired' OR hired_at IS NOT NULL)");
            t.HasCheckConstraint("ck_candidates_salary", "offered_salary IS NULL OR offered_salary >= 0");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(c => c.LegacyCode).IsUnique().HasFilter("legacy_code IS NOT NULL");
        b.HasIndex(c => new { c.PersonId, c.JobOpeningId }).IsUnique();
        b.HasIndex(c => new { c.JobOpeningId, c.Stage });
        b.HasIndex(c => c.RecruiterId);
        b.Property(c => c.CapturedAt).HasDefaultValueSql("current_date");
        b.Property(c => c.Stage).HasDefaultValue(CandidateStage.New).HasSentinel(CandidateStage.New);
        b.Property(c => c.IsHired).HasDefaultValue(false).HasSentinel(false);
        b.Property(c => c.OfferedSalary).HasPrecision(12, 2);
        b.HasOne<Person>().WithMany().HasForeignKey(c => c.PersonId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JobOpening>().WithMany().HasForeignKey(c => c.JobOpeningId).HasPrincipalKey(o => o.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.RecruiterId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CandidateStageChangeConfiguration : IEntityTypeConfiguration<CandidateStageChange>
{
    public void Configure(EntityTypeBuilder<CandidateStageChange> b)
    {
        b.ToTable("candidate_stage_history");
        b.HasIdentityAndUid();
        b.Property(h => h.ChangedAt).HasDefaultValueSql("now()");
        b.HasIndex(h => new { h.CandidateId, h.ChangedAt });
        b.HasOne<Candidate>().WithMany().HasForeignKey(h => h.CandidateId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(h => h.ChangedBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(h => h.ActingForUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CandidateNoteConfiguration : IEntityTypeConfiguration<CandidateNote>
{
    public void Configure(EntityTypeBuilder<CandidateNote> b)
    {
        b.ToTable("candidate_notes", t => t.HasCheckConstraint("ck_candidate_notes_html", "length(content_html) BETWEEN 1 AND 20000"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(n => n.ContentJson).HasColumnType("jsonb");
        b.HasIndex(n => new { n.CandidateId, n.CreatedAt });
        b.HasOne<Candidate>().WithMany().HasForeignKey(n => n.CandidateId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(n => n.AuthorUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(n => n.ActingForUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> b)
    {
        b.ToTable("interviews");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(i => i.Outcome).HasDefaultValue(InterviewOutcome.Pending).HasSentinel(InterviewOutcome.Pending);
        b.HasIndex(i => new { i.CandidateId, i.ScheduledAt });
        b.HasOne<Candidate>().WithMany().HasForeignKey(i => i.CandidateId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.InterviewerId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.ActingForUserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
