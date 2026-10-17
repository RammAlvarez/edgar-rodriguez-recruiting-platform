using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Platform.Core.Users;
using Platform.Tests.Recruiting;
using Platform.Tests.Support;

namespace Platform.Tests.Hosting;

/// <summary>The API host with small limits; every test signs in new people and uses its own client address.</summary>
public sealed class RateLimitedApiFactory : ApiFactory
{
    public const int PerIp = 12;
    public const int Portal = 5;
    public const int Staff = 8;
    public const int Route = 2;

    protected override IReadOnlyDictionary<string, string> RateLimits { get; } = new Dictionary<string, string>
    {
        ["RATE_LIMIT_IP_PER_MINUTE"] = $"{PerIp}",
        ["RATE_LIMIT_PORTAL_PER_MINUTE"] = $"{Portal}",
        ["RATE_LIMIT_STAFF_PER_MINUTE"] = $"{Staff}",
        ["RATE_LIMIT_UPLOADS_PER_HOUR"] = $"{Route}",
        ["RATE_LIMIT_BANK_ACCOUNTS_PER_HOUR"] = $"{Route}",
        ["RATE_LIMIT_PROFILE_PER_10_MINUTES"] = $"{Route}",
        ["RATE_LIMIT_COMMENTS_PER_HOUR"] = $"{Route}",
        ["TRUSTED_PROXY_HOPS"] = "1",
    };
}

/// <summary>
/// PLAN D92: per client address before the token is checked, per person after it, and the four candidate/client routes;
/// 429 in the problem shape with <c>Retry-After</c>, readable by the apps (CORS); <c>/health</c> and <c>/internal</c> never.
/// </summary>
public sealed class RateLimitTests(RateLimitedApiFactory factory) : ApiTest(factory), IClassFixture<RateLimitedApiFactory>
{
    private readonly string _ip = $"203.0.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

    /// <summary>Each test its own address, so the per-address limit never mixes tests.</summary>
    private HttpClient From(HttpClient http, string? forwardedFor = null)
    {
        http.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor ?? _ip);
        return http;
    }

    private async Task<HttpClient> Candidate()
    {
        var admin = From(await AsOwner());
        var heb = await WithDb(db => db.AddClient($"HEB-{Guid.NewGuid():N}"));
        var opening = (await BodyOf(await admin.PostAsJsonAsync("/v1/openings", OpeningTests.NewOpening(heb.Uid))))
            .GetProperty("id").GetGuid();
        var email = $"cand-{Guid.NewGuid():N}@example.test";
        var captured = await admin.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(opening, new
        {
            firstNames = "Rosa",
            lastNamePaternal = "Leal",
            lastNameMaternal = "Garza",
            isForeign = false,
            email,
            source = "whatsapp",
        }));
        Assert.Equal(HttpStatusCode.OK, captured.StatusCode);
        // The owner's calls above count against this address: the candidate gets one of its own.
        return From(AsEmail(email), $"198.51.100.{Random.Shared.Next(1, 255)}");
    }

    private static async Task<List<HttpStatusCode>> Repeat(int times, Func<Task<HttpResponseMessage>> call)
    {
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < times; i++)
            codes.Add((await call()).StatusCode);
        return codes;
    }

    [Fact]
    public async Task A_candidate_past_the_per_minute_limit_gets_429_with_retry_after_and_a_spanish_message()
    {
        var me = await Candidate();
        Assert.All(await Repeat(RateLimitedApiFactory.Portal, () => me.GetAsync("/v1/candidate-portal/profile")),
            c => Assert.Equal(HttpStatusCode.OK, c));

        var limited = await me.GetAsync("/v1/candidate-portal/profile");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.InRange(int.Parse(limited.Headers.GetValues("Retry-After").Single(), CultureInfo.InvariantCulture), 1, 60);
        var body = await BodyOf(limited);
        Assert.Equal("Demasiadas solicitudes", body.GetProperty("title").GetString());
        Assert.StartsWith("Hiciste demasiadas solicitudes seguidas.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Staff_have_the_higher_limit()
    {
        var recruiter = From(await As(UserRole.Recruiter));
        Assert.All(await Repeat(RateLimitedApiFactory.Staff, () => recruiter.GetAsync("/v1/me")), c => Assert.Equal(HttpStatusCode.OK, c));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await recruiter.GetAsync("/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Each_candidate_route_has_its_own_hourly_limit_and_staff_are_not_bound_by_it()
    {
        var me = await Candidate();
        // The limit counts the call whatever its answer: these bodies are refused (400/404) and still count.
        var upload = await Repeat(RateLimitedApiFactory.Route + 1, () => me.PostAsJsonAsync("/v1/files/presign-upload", new { }));
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, upload[..^1]);
        Assert.Equal(HttpStatusCode.TooManyRequests, upload[^1]);

        // Another route of the same candidate still answers (one call left of the per-minute 5).
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await me.PostAsJsonAsync("/v1/candidate-portal/bank-accounts", new { })).StatusCode);

        var recruiter = From(await As(UserRole.Recruiter), "192.0.2.10");
        var staff = await Repeat(RateLimitedApiFactory.Route + 2, () => recruiter.PostAsJsonAsync("/v1/files/presign-upload", new { }));
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, staff);
    }

    [Fact]
    public async Task Profile_and_bank_account_writes_are_limited_per_candidate()
    {
        var profile = await Candidate();
        var edits = await Repeat(RateLimitedApiFactory.Route + 1, () => profile.PutAsJsonAsync("/v1/candidate-portal/profile", new { }));
        Assert.Equal(HttpStatusCode.TooManyRequests, edits[^1]);
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, edits[..^1]);

        var accounts = await Candidate();
        var adds = await Repeat(RateLimitedApiFactory.Route + 1, () => accounts.PostAsJsonAsync("/v1/candidate-portal/bank-accounts", new { }));
        Assert.Equal(HttpStatusCode.TooManyRequests, adds[^1]);
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, adds[..^1]);
    }

    [Fact]
    public async Task A_client_is_limited_on_comments()
    {
        var client = From(await As(UserRole.Client));
        var posts = await Repeat(RateLimitedApiFactory.Route + 1,
            () => client.PostAsJsonAsync($"/v1/client-portal/openings/{Guid.NewGuid()}/comments", new { body = "Hola" }));
        Assert.Equal([HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests], posts);
    }

    [Fact]
    public async Task Forged_tokens_are_limited_per_address_and_a_forged_forwarded_header_does_not_reset_it()
    {
        var junk = From(Client());
        junk.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-token");
        Assert.All(await Repeat(RateLimitedApiFactory.PerIp, () => junk.GetAsync("/v1/me")), c => Assert.Equal(HttpStatusCode.Unauthorized, c));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await junk.GetAsync("/v1/me")).StatusCode);

        // Only the entry the proxy appended (the rightmost) is believed.
        var spoofed = From(Client(), $"10.9.8.7, {_ip}");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await spoofed.GetAsync("/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await From(Client(), "192.0.2.77").GetAsync("/v1/me")).StatusCode);
    }

    [Fact]
    public async Task The_429_carries_cors_so_the_apps_can_read_it()
    {
        var junk = From(Client());
        junk.DefaultRequestHeaders.Add("Origin", "http://localhost:4301");
        await Repeat(RateLimitedApiFactory.PerIp, () => junk.GetAsync("/v1/me"));
        var limited = await junk.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("http://localhost:4301", limited.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Health_and_internal_are_never_limited()
    {
        var http = From(Client());
        var health = await Repeat(RateLimitedApiFactory.PerIp + 3, () => http.GetAsync("/health"));
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, health);
        var jobs = await Repeat(RateLimitedApiFactory.PerIp + 3, () => http.PostAsync("/internal/jobs/no-such-job", null));
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, jobs);
    }
}
