using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Security;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Access;

/// <summary>
/// Verificación en dos pasos in the API (S12, SPEC §2.3, PLAN D89): RFC 6238 codes, enrollment, a new sign-in asked again,
/// one use per code, recovery codes, the wrong-code lock, «requerida» from Usuarios and «Restablecer».
/// </summary>
public sealed class MfaTests(ApiFactory factory) : ApiTest(factory)
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);

    private HttpClient SignIn(User user, long authTime)
    {
        var http = Factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", FakeAuthProvider.TokenFor(user.AuthUid, authTime));
        return http;
    }

    private static string CodeAt(string secret, DateTimeOffset at) => Totp.Code(Base32.Decode(secret), Totp.Step(at));

    private async Task<(string Secret, List<string> Recovery)> Enroll(HttpClient http)
    {
        var setup = await BodyOf(await http.PostAsync("/v1/me/mfa/setup", null));
        var secret = setup.GetProperty("secret").GetString()!;
        Assert.StartsWith("otpauth://totp/Plataforma%20RyP:", setup.GetProperty("otpAuthUri").GetString(), StringComparison.Ordinal);
        var enabled = await http.PostAsJsonAsync("/v1/me/mfa/enable", new { code = CodeAt(secret, Factory.Clock.UtcNow) });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        return (secret, (await BodyOf(enabled)).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToList());
    }

    private static async Task<string?> ProblemType(HttpResponseMessage r) =>
        (await BodyOf(r)).TryGetProperty("type", out var t) ? t.GetString() : null;

    [Fact]
    public void Codes_follow_the_rfc_6238_test_vectors()
    {
        var secret = Encoding.ASCII.GetBytes("12345678901234567890");
        Assert.Equal("94287082", Totp.Code(secret, Totp.Step(DateTimeOffset.FromUnixTimeSeconds(59)), 8));
        Assert.Equal("07081804", Totp.Code(secret, Totp.Step(DateTimeOffset.FromUnixTimeSeconds(1111111109)), 8));
        Assert.Equal("14050471", Totp.Code(secret, Totp.Step(DateTimeOffset.FromUnixTimeSeconds(1111111111)), 8));
        Assert.Equal(secret, Base32.Decode(Base32.Encode(secret)));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Base32.Encode(secret));
    }

    [Fact]
    public async Task Turning_it_on_asks_the_code_at_each_new_sign_in_and_each_code_works_once()
    {
        Factory.Clock.Now = T0;
        var (user, _) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollPrepayroll });
        var first = SignIn(user, 100);
        Assert.Equal(MfaEndpoints.StartFirst, await DetailOf(await first.PostAsJsonAsync("/v1/me/mfa/enable", new { code = "000000" })));
        var (secret, recovery) = await Enroll(first);
        Assert.Equal(8, recovery.Count);
        var me = await BodyOf(await first.GetAsync("/v1/me"));
        Assert.Equal((true, true), (me.GetProperty("mfaEnabled").GetBoolean(), me.GetProperty("mfaVerified").GetBoolean()));
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/v1/payroll/periods")).StatusCode);

        // A new sign-in: everything but /me and /me/mfa waits for the code.
        var second = SignIn(user, 200);
        var refused = await second.GetAsync("/v1/payroll/periods");
        Assert.Equal((HttpStatusCode.Forbidden, MfaEndpoints.ProblemType), (refused.StatusCode, await ProblemType(refused)));
        Assert.False((await BodyOf(await second.GetAsync("/v1/me"))).GetProperty("mfaVerified").GetBoolean());
        // The code that turned it on was used: the same step is refused, the next one passes.
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsJsonAsync("/v1/me/mfa/verify", new { code = CodeAt(secret, T0) })).StatusCode);
        Factory.Clock.Now = T0.AddSeconds(30);
        Assert.Equal(HttpStatusCode.NoContent, (await second.PostAsJsonAsync("/v1/me/mfa/verify", new { code = CodeAt(secret, T0.AddSeconds(30)) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/v1/payroll/periods")).StatusCode);

        // A recovery code passes once, written any way.
        var third = SignIn(user, 300);
        Assert.Equal(HttpStatusCode.NoContent, (await third.PostAsJsonAsync("/v1/me/mfa/verify", new { code = recovery[0].ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal) })).StatusCode);
        var fourth = SignIn(user, 400);
        Assert.Equal(HttpStatusCode.BadRequest, (await fourth.PostAsJsonAsync("/v1/me/mfa/verify", new { code = recovery[0] })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fourth.PostAsJsonAsync("/v1/me/mfa/verify", new { code = recovery[1] })).StatusCode);

        // Turned off with a code (it is optional for this user); then nothing is asked.
        Factory.Clock.Now = T0.AddSeconds(90);
        Assert.Equal(HttpStatusCode.NoContent, (await fourth.PostAsJsonAsync("/v1/me/mfa/disable", new { code = CodeAt(secret, T0.AddSeconds(90)) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignIn(user, 500).GetAsync("/v1/payroll/periods")).StatusCode);
    }

    [Fact]
    public async Task Five_wrong_codes_wait_fifteen_minutes()
    {
        Factory.Clock.Now = T0;
        var (user, _) = await SignedIn(UserRole.Recruiter);
        var (secret, _) = await Enroll(SignIn(user, 100));
        var next = SignIn(user, 200);
        Factory.Clock.Now = T0.AddSeconds(60);
        for (var i = 0; i < 5; i++)
            await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = "123456" });
        var locked = await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = CodeAt(secret, T0.AddSeconds(60)) });
        Assert.Equal([MfaEndpoints.Locked], (await ErrorsOf(locked))["code"]);
        Factory.Clock.Now = T0.AddMinutes(16);
        Assert.Equal(HttpStatusCode.NoContent, (await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = CodeAt(secret, T0.AddMinutes(16)) })).StatusCode);
    }

    [Fact]
    public async Task After_the_wait_a_wrong_code_counts_as_the_first_of_a_new_window()
    {
        Factory.Clock.Now = T0;
        var (user, _) = await SignedIn(UserRole.Recruiter);
        await Enroll(SignIn(user, 100));
        var next = SignIn(user, 200);
        Factory.Clock.Now = T0.AddSeconds(60);
        for (var i = 0; i < 5; i++)
            await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = "123456" });
        Factory.Clock.Now = T0.AddMinutes(16);
        Assert.Equal([MfaEndpoints.WrongCode], (await ErrorsOf(await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = "123456" })))["code"]);
        Assert.Equal(1, await WithDb(db => db.Set<User>().Where(u => u.Uid == user.Uid).Select(u => u.MfaFailedAttempts).SingleAsync()));
    }

    [Fact]
    public async Task A_full_count_without_a_wait_does_not_lock_for_good()
    {
        // What a check that died between its charge and its lock UPDATE used to leave behind.
        Factory.Clock.Now = T0;
        var (user, _) = await SignedIn(UserRole.Recruiter);
        var (secret, _) = await Enroll(SignIn(user, 100));
        await WithDb(db => db.Set<User>().Where(u => u.Uid == user.Uid).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.MfaFailedAttempts, MfaEndpoints.MaxAttempts).SetProperty(u => u.MfaLockedUntil, (DateTimeOffset?)null)));
        Factory.Clock.Now = T0.AddSeconds(60);
        Assert.Equal(HttpStatusCode.NoContent, (await SignIn(user, 200).PostAsJsonAsync("/v1/me/mfa/verify", new { code = CodeAt(secret, T0.AddSeconds(60)) })).StatusCode);
    }

    [Fact]
    public async Task The_last_attempt_starts_the_wait_before_the_code_is_checked()
    {
        Factory.Clock.Now = T0;
        var (user, _) = await SignedIn(UserRole.Recruiter);
        await Enroll(SignIn(user, 100));
        var next = SignIn(user, 200);
        Factory.Clock.Now = T0.AddSeconds(60);
        for (var i = 0; i < 4; i++)
            await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = "123456" });
        Assert.Null(await WithDb(db => db.Set<User>().Where(u => u.Uid == user.Uid).Select(u => u.MfaLockedUntil).SingleAsync()));
        Assert.Equal([MfaEndpoints.Locked], (await ErrorsOf(await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = "123456" })))["code"]);
        Assert.Equal(T0.AddSeconds(60) + MfaEndpoints.LockFor,
            await WithDb(db => db.Set<User>().Where(u => u.Uid == user.Uid).Select(u => u.MfaLockedUntil).SingleAsync()));
    }

    [Fact]
    public async Task Parallel_wrong_codes_still_lock_after_five()
    {
        Factory.Clock.Now = T0;
        var (user, _) = await SignedIn(UserRole.Recruiter);
        var (secret, _) = await Enroll(SignIn(user, 100));
        var next = SignIn(user, 200);
        Factory.Clock.Now = T0.AddSeconds(60);
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = "123456" })));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        var wrong = 0;
        foreach (var r in results)
            wrong += (await ErrorsOf(r))["code"].SequenceEqual([MfaEndpoints.WrongCode]) ? 1 : 0;
        Assert.True(wrong <= MfaEndpoints.MaxAttempts, $"{wrong} wrong codes were evaluated");
        var locked = await next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = CodeAt(secret, T0.AddSeconds(60)) });
        Assert.Equal([MfaEndpoints.Locked], (await ErrorsOf(locked))["code"]);
    }

    [Fact]
    public async Task One_code_and_one_recovery_code_are_accepted_once_in_parallel()
    {
        Factory.Clock.Now = T0;
        var (user, _) = await SignedIn(UserRole.Recruiter);
        var (secret, recovery) = await Enroll(SignIn(user, 100));
        var next = SignIn(user, 200);
        Factory.Clock.Now = T0.AddSeconds(60);
        var totp = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = CodeAt(secret, T0.AddSeconds(60)) })));
        Assert.Equal(1, totp.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        var rec = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => next.PostAsJsonAsync("/v1/me/mfa/verify", new { code = recovery[0] })));
        Assert.Equal(1, rec.Count(r => r.StatusCode == HttpStatusCode.NoContent));
    }

    [Fact]
    public async Task Required_from_usuarios_means_enrolling_first_and_restablecer_starts_over()
    {
        Factory.Clock.Now = T0;
        var owner = await AsOwner();
        var (user, _) = await SignedIn(UserRole.Recruiter);
        await WithDb(db => db.Set<User>().Where(u => u.Uid == user.Uid).ExecuteUpdateAsync(s => s.SetProperty(u => u.MfaRequired, true)));
        var http = SignIn(user, 100);
        var refused = await http.GetAsync("/v1/openings");
        Assert.Equal((MfaEndpoints.ProblemType, MfaEndpoints.MustEnroll), (await ProblemType(refused), await DetailOf(await http.GetAsync("/v1/openings"))));
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/v1/me")).StatusCode);
        var (secret, _) = await Enroll(http);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/v1/openings")).StatusCode);
        Factory.Clock.Now = T0.AddSeconds(30);
        Assert.Equal(MfaEndpoints.Required, await DetailOf(await http.PostAsJsonAsync("/v1/me/mfa/disable", new { code = CodeAt(secret, T0.AddSeconds(30)) })));

        // Lost phone: Usuarios resets it; the next request asks to enroll again.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/v1/users/{user.Uid}/mfa/reset", null)).StatusCode);
        Assert.Equal(MfaEndpoints.MustEnroll, await DetailOf(await http.GetAsync("/v1/openings")));
        Assert.Equal(0, await WithDb(db => db.Set<MfaRecoveryCode>().CountAsync()));
    }

    [Fact]
    public async Task Client_users_and_candidates_are_not_asked()
    {
        var client = await WithDb(db => db.AddClient("HEB"));
        var user = await WithDb(async db =>
        {
            var u = await db.AddUser(UserRole.Client, "Cliente HEB", clientId: client.Uid);
            u.MfaRequired = true;
            await db.SaveChangesAsync();
            return u;
        });
        Assert.Equal(HttpStatusCode.OK, (await Client(user.AuthUid).GetAsync("/v1/client-portal/openings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(user.AuthUid).PostAsync("/v1/me/mfa/setup", null)).StatusCode);
    }
}
