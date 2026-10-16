using Microsoft.EntityFrameworkCore;
using Platform.Core.Audit;
using Platform.Core.Users;

namespace Platform.Infrastructure.Persistence;

/// <summary>
/// The one DbContext of <c>platform-api</c> (schema <c>platform</c>, D35). Each module project owns its entities and
/// EF configurations; this context only assembles them and owns the migrations (PLAN D45).
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string Schema = "platform";

    /// <summary>Assemblies whose <see cref="IEntityTypeConfiguration{TEntity}"/> types make up the model.</summary>
    public static readonly System.Reflection.Assembly[] ModuleAssemblies =
    [
        typeof(User).Assembly, // Platform.Core
    ];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresEnum<UserRole>(Schema, "user_role");
        modelBuilder.HasPostgresEnum<PermissionModule>(Schema, "permission_module");
        modelBuilder.HasPostgresEnum<AuditAction>(Schema, "audit_action");
        modelBuilder.HasPostgresEnum<DataAccessKind>(Schema, "data_access_kind");
        foreach (var assembly in ModuleAssemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
    }
}
