using Platform.Api.Auth;
using Platform.Core.Abstractions;
using Platform.Core.Security;
using Platform.Infrastructure.Email;
using Platform.Infrastructure.Storage;

namespace Platform.Api.Hosting;

/// <summary>All configuration comes from environment variables (no secret managers, D34). See <c>.env.example</c>.</summary>
public sealed record PlatformSettings(
    string DatabaseUrl,
    string InternalSecret,
    IReadOnlyList<string> AppOrigins,
    FirebaseSettings Firebase,
    S3Settings S3,
    EmailSettings Email,
    AppLinks Links,
    EncryptionSettings Encryption,
    RateLimitSettings RateLimits)
{
    public static PlatformSettings FromConfiguration(IConfiguration c, bool allowDefaults)
    {
        string Get(string key, string devDefault) =>
            c[key] is { Length: > 0 } value ? value
            : allowDefaults ? devDefault
            : throw new InvalidOperationException($"Environment variable {key} is required.");

        return new PlatformSettings(
            Get("DATABASE_URL", "Host=localhost;Port=5433;Database=platform;Username=platform_api;Password=platform_api"),
            Get("INTERNAL_SECRET", "local-internal-secret-change-me-0123456789"),
            Get("APP_ORIGINS", "http://localhost:4300,http://localhost:4301,http://localhost:4302")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            new FirebaseSettings(Get("FIREBASE_PROJECT_ID", "demo-platform"), c["FIREBASE_SERVICE_ACCOUNT_JSON"]),
            new S3Settings(
                Get("S3_ENDPOINT", "http://localhost:9000"),
                Get("S3_REGION", "us-east-1"),
                Get("S3_ACCESS_KEY_ID", "minioadmin"),
                Get("S3_SECRET_ACCESS_KEY", "minioadmin"),
                Get("S3_BUCKET_DOCUMENTS", "documents"),
                Get("S3_BUCKET_EXPORTS", "exports"),
                bool.Parse(Get("S3_FORCE_PATH_STYLE", "true"))),
            // Locally an empty key logs the emails instead of sending them (LogEmailSender).
            new EmailSettings(Get("EMAIL_FROM", "Plataforma (local) <no-reply@localhost>"), allowDefaults ? c["RESEND_API_KEY"] : Get("RESEND_API_KEY", "")),
            new AppLinks(
                Get("ADMIN_APP_URL", "http://localhost:4300"),
                Get("CANDIDATES_APP_URL", "http://localhost:4301"),
                Get("CLIENTS_APP_URL", "http://localhost:4302")),
            // The local key is public (it is in this file): it only ever protects sample data on a developer's machine.
            EncryptionSettings.Parse(Get("ENCRYPTION_KEY", "bG9jYWwtb25seS1rZXktbm90LWZvci1yZWFsLWRhdGE="), c["ENCRYPTION_KEY_ID"]),
            RateLimitSettings.FromConfiguration(c));
    }
}
