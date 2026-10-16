using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Platform.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c>. Reads <c>MIGRATIONS_DATABASE_URL</c> (owner role), falling back to <c>DATABASE_URL</c>.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var url = Environment.GetEnvironmentVariable("MIGRATIONS_DATABASE_URL")
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? "Host=localhost;Port=5433;Database=platform;Username=platform_owner;Password=platform_owner";
        var options = new DbContextOptionsBuilder<PlatformDbContext>();
        PersistenceServices.Configure(options, PersistenceServices.BuildDataSource(PostgresConnection.ToConnectionString(url)));
        return new PlatformDbContext(options.Options);
    }
}
