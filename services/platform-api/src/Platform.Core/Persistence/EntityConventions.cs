using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Abstractions;

namespace Platform.Core.Persistence;

/// <summary>Plain helpers every EF configuration calls — composition instead of a base entity.</summary>
public static class EntityConventions
{
    /// <summary><c>id bigint generated always as identity</c> (internal) + <c>uid uuid unique default gen_random_uuid()</c> (public, FK target).</summary>
    public static void HasIdentityAndUid<T>(this EntityTypeBuilder<T> b) where T : class
    {
        b.Property<long>("Id").UseIdentityAlwaysColumn();
        b.HasKey("Id");
        // The entity creates its own uid (Guid.CreateVersion7) so the audit trail and FKs know it before the insert;
        // the column default only serves raw SQL inserts.
        b.Property<Guid>("Uid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedNever();
        b.HasAlternateKey("Uid");
    }

    public static void HasStamps<T>(this EntityTypeBuilder<T> b) where T : class, IStamped
    {
        b.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        b.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
    }
}
