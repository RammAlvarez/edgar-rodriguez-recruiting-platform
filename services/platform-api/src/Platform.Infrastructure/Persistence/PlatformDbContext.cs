using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
        typeof(Platform.Recruiting.Openings.JobOpening).Assembly, // Platform.Recruiting
        typeof(Platform.Payroll.Staff.Employee).Assembly, // Platform.Payroll
        typeof(Platform.Bank.Payments.PaymentBatch).Assembly, // Platform.Bank
    ];

    /// <summary>
    /// Npgsql only writes offset 0 to <c>timestamptz</c>: a date-time a client sent as «-06:00» was a 500 (E2E run, D-031). Every
    /// <see cref="DateTimeOffset"/> is stored as the same instant in UTC.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

    private sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");
        // Exclusion constraint of employee_status_periods (uuid equality in a GiST index).
        modelBuilder.HasPostgresExtension("btree_gist");
        foreach (var e in PostgresEnums.All)
            e.Declare(modelBuilder);
        foreach (var assembly in ModuleAssemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
    }
}
