using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Users;

namespace Platform.Api.Hosting;

/// <summary>
/// Limits of PLAN D92 (env <c>RATE_LIMIT_*</c>; 0 turns that limit off). Counters live in each API instance's memory, so
/// with several instances a person can reach the limit once per instance.
/// </summary>
/// <param name="TrustedProxyHops">
/// Proxies in front of the API that append to <c>X-Forwarded-For</c> (Caddy or a provider's front end: 1). Only that many
/// entries from the right are believed, so a header the browser forges is ignored. 0 uses the connection's address.
/// </param>
public sealed record RateLimitSettings(
    int PerIpPerMinute,
    int PortalPerMinute,
    int StaffPerMinute,
    int UploadsPerHour,
    int BankAccountsPerHour,
    int ProfilePer10Minutes,
    int CommentsPerHour,
    int TrustedProxyHops)
{
    public static RateLimitSettings FromConfiguration(IConfiguration c)
    {
        int Get(string key, int fallback) =>
            c[key] is not { Length: > 0 } raw ? fallback
            : int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value
            : throw new InvalidOperationException($"Environment variable {key} must be a whole number ≥ 0.");

        return new RateLimitSettings(
            Get("RATE_LIMIT_IP_PER_MINUTE", 600),
            Get("RATE_LIMIT_PORTAL_PER_MINUTE", 120),
            Get("RATE_LIMIT_STAFF_PER_MINUTE", 600),
            Get("RATE_LIMIT_UPLOADS_PER_HOUR", 30),
            Get("RATE_LIMIT_BANK_ACCOUNTS_PER_HOUR", 10),
            Get("RATE_LIMIT_PROFILE_PER_10_MINUTES", 30),
            Get("RATE_LIMIT_COMMENTS_PER_HOUR", 30),
            Get("TRUSTED_PROXY_HOPS", 1));
    }
}

/// <summary>
/// Two layers (PLAN D92): <see cref="IpRateLimitMiddleware"/> per client address before the token is checked (junk and
/// forged tokens), then ASP.NET's limiter after <c>AuthenticationMiddleware</c>, per signed-in person, plus the named
/// <see cref="RateLimitPolicies"/>. Only <c>/v1</c> is limited: never <c>/health</c> nor the scheduler's <c>/internal</c>.
/// </summary>
public static partial class RateLimiting
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddPlatformRateLimiting(this IServiceCollection services, RateLimitSettings s)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            o.ForwardLimit = s.TrustedProxyHops;
            // The proxies' own addresses change (containers, the providers' front ends): the hop count is the trust.
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
        services.AddSingleton(new IpRateLimiter(PartitionedRateLimiter.Create<HttpContext, string>(http =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return Window($"ip:{ip}", s.PerIpPerMinute, Minute);
        })));
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (context, ct) => Reject(context.HttpContext, context.Lease, ct);
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                Who(http) switch
                {
                    { Staff: true } who => Window(who.Key, s.StaffPerMinute, Minute),
                    { } who => Window(who.Key, s.PortalPerMinute, Minute),
                    // No principal: the route answers 401, and the per-address limit already counted it.
                    null => RateLimitPartition.GetNoLimiter(""),
                });
            o.AddPolicy(RateLimitPolicies.Uploads, PortalOnly(s.UploadsPerHour, TimeSpan.FromHours(1)));
            o.AddPolicy(RateLimitPolicies.BankAccounts, PortalOnly(s.BankAccountsPerHour, TimeSpan.FromHours(1)));
            o.AddPolicy(RateLimitPolicies.Profile, PortalOnly(s.ProfilePer10Minutes, TimeSpan.FromMinutes(10)));
            o.AddPolicy(RateLimitPolicies.Comments, PortalOnly(s.CommentsPerHour, TimeSpan.FromHours(1)));
        });
        return services;
    }

    /// <summary>The partition of the signed-in person (an Administrador covering for a recruiter counts as themself).</summary>
    private static (string Key, bool Staff)? Who(HttpContext http)
    {
        if (!http.Request.Path.StartsWithSegments("/v1"))
            return null;
        var ctx = http.RequestServices.GetRequiredService<RequestContext>();
        if (ctx.Candidate is { } candidate)
            return ($"candidate:{candidate.PersonId}", false);
        if (ctx.Principal is { } user)
            return user.Role == UserRole.Client ? ($"client:{user.UserId}", false) : ($"staff:{user.UserId}", true);
        return null;
    }

    private static Func<HttpContext, RateLimitPartition<string>> PortalOnly(int permits, TimeSpan window) => http =>
        Who(http) is { Staff: false } who ? Window(who.Key, permits, window) : RateLimitPartition.GetNoLimiter("");

    private static RateLimitPartition<string> Window(string key, int permits, TimeSpan window) =>
        permits <= 0
            ? RateLimitPartition.GetNoLimiter(key)
            : RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = window,
                QueueLimit = 0,
            });

    /// <summary>429 in the api-client's problem shape, with <c>Retry-After</c> and the wait in the message.</summary>
    internal static async ValueTask Reject(HttpContext http, RateLimitLease lease, CancellationToken ct)
    {
        var wait = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : Minute;
        var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        var ctx = http.RequestServices.GetRequiredService<RequestContext>();
        LogRejected(http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RateLimiting).FullName!),
            http.Request.Method, http.Request.Path.Value,
            ctx.Candidate is { } c ? $"candidate:{c.PersonId}" : ctx.Principal is { } p ? $"user:{p.UserId}" : "anonymous",
            http.Connection.RemoteIpAddress?.ToString(), seconds);
        var when = seconds < 60 ? "en unos segundos" : seconds < 120 ? "en un minuto" : $"en {(seconds + 59) / 60} minutos";
        await TypedResults.Problem(title: "Demasiadas solicitudes",
                detail: $"Hiciste demasiadas solicitudes seguidas. Inténtalo de nuevo {when}.",
                statusCode: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(http);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rate limit hit on {Method} {RequestPath} by {Who} from {RemoteIp}; retry after {RetryAfterSeconds}s")]
    private static partial void LogRejected(ILogger log, string method, string? requestPath, string who, string? remoteIp,
        int retryAfterSeconds);
}

/// <summary>The per-address limiter, as a singleton so its counters outlive the request.</summary>
public sealed record IpRateLimiter(PartitionedRateLimiter<HttpContext> Limiter);

/// <summary>Per client address on <c>/v1</c>, before the bearer token is verified (PLAN D92).</summary>
public sealed class IpRateLimitMiddleware(RequestDelegate next, IpRateLimiter limiter)
{
    public async Task InvokeAsync(HttpContext http)
    {
        if (!http.Request.Path.StartsWithSegments("/v1"))
        {
            await next(http);
            return;
        }
        using var lease = await limiter.Limiter.AcquireAsync(http, 1, http.RequestAborted);
        if (!lease.IsAcquired)
        {
            await RateLimiting.Reject(http, lease, http.RequestAborted);
            return;
        }
        await next(http);
    }
}
