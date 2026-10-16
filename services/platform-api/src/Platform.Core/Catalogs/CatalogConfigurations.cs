using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Persistence;

namespace Platform.Core.Catalogs;

internal sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> b)
    {
        b.ToTable("zones");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(z => new { z.State, z.NameEs }).IsUnique();
        b.Property(z => z.IsActive).HasDefaultValue(true);
    }
}

internal sealed class CatalogPositionConfiguration : IEntityTypeConfiguration<CatalogPosition>
{
    public void Configure(EntityTypeBuilder<CatalogPosition> b)
    {
        b.ToTable("positions_catalog");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(p => p.NameEs).IsUnique();
        b.Property(p => p.IsActive).HasDefaultValue(true);
    }
}

internal sealed class EducationLevelConfiguration : IEntityTypeConfiguration<EducationLevel>
{
    public void Configure(EntityTypeBuilder<EducationLevel> b)
    {
        b.ToTable("education_levels");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(e => e.NameEs).IsUnique();
        b.Property(e => e.IsActive).HasDefaultValue(true);
    }
}

internal sealed class BenefitConfiguration : IEntityTypeConfiguration<Benefit>
{
    public void Configure(EntityTypeBuilder<Benefit> b)
    {
        b.ToTable("benefits");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(e => e.NameEs).IsUnique();
        b.Property(e => e.IsActive).HasDefaultValue(true);
    }
}

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("holidays");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(h => h.Date).IsUnique();
    }
}

internal sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> b)
    {
        b.ToTable("banks", t => t.HasCheckConstraint("ck_banks_spei_code", @"spei_code ~ '^\d{3}$'"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(x => x.SpeiCode).IsUnique();
        b.Property(x => x.IsActive).HasDefaultValue(true);
    }
}

internal sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> b)
    {
        b.ToTable("document_types", t =>
        {
            t.HasCheckConstraint("ck_document_types_code", "code ~ '^[A-Z][A-Z0-9_]*$'");
            t.HasCheckConstraint("ck_document_types_validity", "default_validity_days IS NULL OR (has_expiry AND default_validity_days > 0)");
        });
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.IsActive).HasDefaultValue(true);
    }
}

internal sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> b)
    {
        b.ToTable("document_templates");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(x => x.NameEs).IsUnique();
        // One default checklist per scope.
        b.HasIndex(x => x.Scope).IsUnique().HasFilter("is_default").HasDatabaseName("ux_document_templates_default");
        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.TemplateId).HasPrincipalKey(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocumentTemplateItemConfiguration : IEntityTypeConfiguration<DocumentTemplateItem>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateItem> b)
    {
        b.ToTable("document_template_items");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(x => new { x.TemplateId, x.DocumentTypeId }).IsUnique();
        b.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId).HasPrincipalKey(t => t.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}
