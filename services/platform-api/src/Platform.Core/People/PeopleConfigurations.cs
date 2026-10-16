using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Catalogs;
using Platform.Core.Persistence;

namespace Platform.Core.People;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.ToTable("people", t =>
        {
            t.HasCheckConstraint("ck_people_curp", $"curp ~ '{MexicanIds.CurpPatternText}'");
            t.HasCheckConstraint("ck_people_rfc", $"rfc ~ '{MexicanIds.PersonRfcPatternText}'");
            t.HasCheckConstraint("ck_people_nss", @"nss ~ '^\d{11}$'");
            t.HasCheckConstraint("ck_people_mobile_phone", @"mobile_phone ~ '^\d{10}$'");
            t.HasCheckConstraint("ck_people_postal_code", @"postal_code ~ '^\d{5}$'");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(p => p.FullName)
            .HasComputedColumnSql("last_name_paternal || ' ' || COALESCE(last_name_maternal || ' ', '') || first_names", stored: true);
        b.Property(p => p.Gender).HasDefaultValue(Gender.Unspecified).HasSentinel(Gender.Unspecified);
        b.Property(p => p.IsForeign).HasDefaultValue(false).HasSentinel(false);
        b.Property(p => p.Email).HasColumnType("citext");
        // Soft-deleted people keep their CURP/RFC: a new capture finds them and the UI offers to restore instead.
        b.HasIndex(p => p.Curp).IsUnique().HasFilter("curp IS NOT NULL");
        b.HasIndex(p => p.Rfc).IsUnique().HasFilter("rfc IS NOT NULL");
        b.HasIndex(p => p.MobilePhone);
        b.HasIndex(p => p.FullName).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("ix_people_full_name_trgm");
    }
}

internal sealed class PersonDocumentConfiguration : IEntityTypeConfiguration<PersonDocument>
{
    public void Configure(EntityTypeBuilder<PersonDocument> b)
    {
        b.ToTable("person_documents", t =>
        {
            t.HasCheckConstraint("ck_person_documents_size", "size_bytes > 0 AND size_bytes <= 10485760");
            t.HasCheckConstraint("ck_person_documents_rejection", "status <> 'rejected' OR rejection_reason IS NOT NULL");
            t.HasCheckConstraint("ck_person_documents_dates", "expires_at IS NULL OR issued_at IS NULL OR expires_at >= issued_at");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(d => d.Status).HasDefaultValue(DocumentStatus.Delivered).HasSentinel(DocumentStatus.Delivered);
        b.Property(d => d.Version).HasDefaultValue(1).HasSentinel(1);
        b.HasIndex(d => d.StorageKey).IsUnique();
        // The current document of each type.
        b.HasIndex(d => new { d.PersonId, d.DocumentTypeId }).IsUnique().HasFilter("superseded_by IS NULL")
            .HasDatabaseName("ux_person_documents_current");
        b.HasIndex(d => d.ExpiresAt).HasFilter("superseded_by IS NULL AND expires_at IS NOT NULL");
        b.HasOne<Person>().WithMany().HasForeignKey(d => d.PersonId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DocumentType>().WithMany().HasForeignKey(d => d.DocumentTypeId).HasPrincipalKey(t => t.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonDocument>().WithMany().HasForeignKey(d => d.SupersededBy).HasPrincipalKey(d => d.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PersonBankAccountConfiguration : IEntityTypeConfiguration<PersonBankAccount>
{
    public void Configure(EntityTypeBuilder<PersonBankAccount> b)
    {
        b.ToTable("person_bank_accounts", t =>
        {
            t.HasCheckConstraint("ck_person_bank_accounts_last4", @"account_last4 ~ '^\d{4}$'");
            t.HasCheckConstraint("ck_person_bank_accounts_holder", "length(holder_name) BETWEEN 1 AND 40");
            t.HasCheckConstraint("ck_person_bank_accounts_validity", "valid_to IS NULL OR valid_to >= valid_from");
            t.HasCheckConstraint("ck_person_bank_accounts_primary_current", "NOT is_primary OR valid_to IS NULL");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(a => a.IsPrimary).HasDefaultValue(false).HasSentinel(false);
        b.Property(a => a.IsFrequentAccountRegistered).HasDefaultValue(false).HasSentinel(false);
        // One primary account per person: the pay goes there (SPEC §5.8).
        b.HasIndex(a => a.PersonId).IsUnique().HasFilter("is_primary").HasDatabaseName("ux_person_bank_accounts_primary");
        b.HasOne<Person>().WithMany().HasForeignKey(a => a.PersonId).HasPrincipalKey(p => p.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Bank>().WithMany().HasForeignKey(a => a.BankId).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
