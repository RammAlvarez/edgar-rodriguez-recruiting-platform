using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Audit;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Users;

namespace Platform.Core.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t => t.HasCheckConstraint(
            "ck_users_client_scope", "(role = 'client') = (client_id IS NOT NULL)"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(u => u.AuthUid).IsUnique();
        b.Property(u => u.Email).HasColumnType("citext");
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.MfaRequired).HasDefaultValue(false).HasSentinel(false);
        b.Property(u => u.MfaFailedAttempts).HasDefaultValue(0).HasSentinel(0);
        b.Property(u => u.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.HasOne<Client>().WithMany().HasForeignKey(u => u.ClientId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(u => u.Permissions).WithOne().HasForeignKey(p => p.UserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasMany<ClientContact>().WithOne().HasForeignKey(c => c.UserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.SetNull);
        b.HasMany<PersonDocument>().WithOne().HasForeignKey(d => d.ValidatedBy).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> b)
    {
        b.ToTable("user_permissions");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(p => new { p.UserId, p.Module, p.ClientId }).IsUnique().AreNullsDistinct(false);
        b.HasOne<Client>().WithMany().HasForeignKey(p => p.ClientId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(c => c.ShortName).IsUnique();
        b.Property(c => c.UsesRecruiting).HasDefaultValue(false).HasSentinel(false);
        b.Property(c => c.UsesPayroll).HasDefaultValue(false).HasSentinel(false);
        b.Property(c => c.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.HasMany(c => c.Contacts).WithOne().HasForeignKey(x => x.ClientId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(c => c.Sites).WithOne().HasForeignKey(x => x.ClientId).HasPrincipalKey(c => c.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ClientContactConfiguration : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> b)
    {
        b.ToTable("client_contacts");
        b.HasIdentityAndUid();
        b.HasStamps();
        b.Property(x => x.Email).HasColumnType("citext");
        // One primary contact per client.
        b.HasIndex(x => x.ClientId).IsUnique().HasFilter("is_primary").HasDatabaseName("ux_client_contacts_primary");
        b.HasIndex(x => x.ClientId);
    }
}

internal sealed class ClientSiteConfiguration : IEntityTypeConfiguration<ClientSite>
{
    public void Configure(EntityTypeBuilder<ClientSite> b)
    {
        b.ToTable("client_sites", t => t.HasCheckConstraint("ck_client_sites_postal_code", @"postal_code IS NULL OR postal_code ~ '^\d{5}$'"));
        b.HasIdentityAndUid();
        b.HasStamps();
        b.HasIndex(x => new { x.ClientId, x.Name }).IsUnique();
        b.Property(x => x.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.HasOne<Zone>().WithMany().HasForeignKey(x => x.ZoneId).HasPrincipalKey(z => z.Uid).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The log tables are created by raw SQL in the migration (monthly partitions, append-only trigger).</summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("audit_log", t => t.ExcludeFromMigrations());
        b.HasKey(e => new { e.Id, e.OccurredAt });
        b.Property(e => e.Id).UseIdentityAlwaysColumn();
        b.Property(e => e.Before).HasColumnType("jsonb");
        b.Property(e => e.After).HasColumnType("jsonb");
    }
}

internal sealed class DataAccessEntryConfiguration : IEntityTypeConfiguration<DataAccessEntry>
{
    public void Configure(EntityTypeBuilder<DataAccessEntry> b)
    {
        b.ToTable("data_access_log", t => t.ExcludeFromMigrations());
        b.HasKey(e => new { e.Id, e.OccurredAt });
        b.Property(e => e.Id).UseIdentityAlwaysColumn();
    }
}
