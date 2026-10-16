using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Core.Audit;
using Platform.Core.Users;

namespace Platform.Infrastructure.Persistence;

public static class PersistenceServices
{
    public static IServiceCollection AddPlatformPersistence(this IServiceCollection services, string databaseUrl)
    {
        var dataSource = BuildDataSource(PostgresConnection.ToConnectionString(databaseUrl));
        services.AddSingleton(dataSource);
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<PlatformDbContext>((sp, options) => Configure(options, dataSource)
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        // Module services depend on EF Core's DbContext, not on this assembly (PLAN D45).
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<PlatformDbContext>());
        return services;
    }

    /// <summary>The Postgres enums must be mapped on the data source as well as in EF (Npgsql 8+).</summary>
    public static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson();
        builder.MapEnum<UserRole>($"{PlatformDbContext.Schema}.user_role");
        builder.MapEnum<PermissionModule>($"{PlatformDbContext.Schema}.permission_module");
        builder.MapEnum<AuditAction>($"{PlatformDbContext.Schema}.audit_action");
        builder.MapEnum<DataAccessKind>($"{PlatformDbContext.Schema}.data_access_kind");
        return builder.Build();
    }

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options
            .UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", PlatformDbContext.Schema);
                npgsql.MapEnum<UserRole>("user_role", PlatformDbContext.Schema);
                npgsql.MapEnum<PermissionModule>("permission_module", PlatformDbContext.Schema);
                npgsql.MapEnum<AuditAction>("audit_action", PlatformDbContext.Schema);
                npgsql.MapEnum<DataAccessKind>("data_access_kind", PlatformDbContext.Schema);
            })
            .UseSnakeCaseNamingConvention();
}
