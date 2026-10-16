using Npgsql;
using Platform.Infrastructure.Persistence.MigrationSql;

namespace Platform.Tests.Support;

/// <summary>
/// The throw-away database (PLAN D48): <c>platform_test</c> on the local compose Postgres. The API connects as
/// <c>platform_api</c> like in production; migrations and truncation run as <c>platform_owner</c>.
/// Port: <c>PLATFORM_DB_PORT</c> from the environment or the repo-root <c>.env</c>, default 5433.
/// </summary>
public static class TestDatabase
{
    public static string Port { get; } = ResolvePort();

    public static string ApiConnectionString =>
        Environment.GetEnvironmentVariable("TEST_DATABASE_URL")
        ?? $"Host=localhost;Port={Port};Database=platform_test;Username=platform_api;Password=platform_api;Include Error Detail=true";

    public static string OwnerConnectionString =>
        Environment.GetEnvironmentVariable("TEST_MIGRATIONS_DATABASE_URL")
        ?? $"Host=localhost;Port={Port};Database=platform_test;Username=platform_owner;Password=platform_owner;Include Error Detail=true";

    /// <summary>
    /// Empties every table of schema <c>platform</c> except the migrations history, then puts back the reference
    /// catalogs the migrations insert (banks, document types, zones, …) as a fresh installation has them.
    /// </summary>
    public static async Task TruncateAsync()
    {
        await using var connection = new NpgsqlConnection(OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DO $$
            DECLARE tables text;
            BEGIN
                SELECT string_agg(format('platform.%I', c.relname), ', ') INTO tables
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'platform' AND c.relkind IN ('r', 'p') AND NOT c.relispartition
                  AND c.relname <> '__ef_migrations_history';
                IF tables IS NOT NULL THEN
                    EXECUTE 'TRUNCATE ' || tables || ' RESTART IDENTITY CASCADE';
                END IF;
            END $$;
            """;
        await command.ExecuteNonQueryAsync();
        command.CommandText = ReferenceDataSql.Up;
        await command.ExecuteNonQueryAsync();
    }

    private static string ResolvePort()
    {
        if (Environment.GetEnvironmentVariable("PLATFORM_DB_PORT") is { Length: > 0 } fromEnv)
            return fromEnv;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var env = Path.Combine(dir.FullName, ".env");
            if (File.Exists(env) && File.Exists(Path.Combine(dir.FullName, "angular.json")))
            {
                var line = File.ReadLines(env).FirstOrDefault(l => l.StartsWith("PLATFORM_DB_PORT=", StringComparison.Ordinal));
                return line is null ? "5433" : line["PLATFORM_DB_PORT=".Length..].Trim();
            }
        }
        return "5433";
    }
}
