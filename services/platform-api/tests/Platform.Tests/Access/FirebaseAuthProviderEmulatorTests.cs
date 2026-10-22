using System.Net.Http.Json;
using System.Text.Json;
using Platform.Api.Auth;

namespace Platform.Tests.Access;

/// <summary>
/// The real Firebase adapter against the Auth emulator (kickoff "Testing"). Runs only when
/// <c>FIREBASE_AUTH_EMULATOR_HOST</c> is set — locally via <c>npm run test:api:emulator</c>, always in CI.
/// </summary>
public sealed class FirebaseAuthProviderEmulatorTests
{
    private const string ProjectId = "demo-platform";
    private static readonly string? EmulatorHost = Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST");

    private readonly FirebaseAuthProvider _provider = new(new FirebaseSettings(ProjectId, null));

    [Fact]
    public async Task Verifies_an_emulator_id_token_and_returns_its_uid_and_email()
    {
        if (EmulatorHost is null)
            return;
        var email = $"{Guid.NewGuid():N}@example.test";
        var (uid, idToken) = await SignUp(email);

        var identity = await _provider.VerifyAsync(idToken, CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Equal(uid, identity.Uid);
        Assert.Equal(email, identity.Email);
        // When that sign-in happened: what a verified MFA code is bound to (PLAN D89).
        Assert.True(identity.AuthTime > 0);
    }

    [Fact]
    public async Task Rejects_a_tampered_token()
    {
        if (EmulatorHost is null)
            return;
        Assert.Null(await _provider.VerifyAsync("eyJhbGciOiJub25lIn0.e30.", CancellationToken.None));
    }

    [Fact]
    public async Task Create_user_is_idempotent_by_email_and_issues_a_password_link()
    {
        if (EmulatorHost is null)
            return;
        var email = $"{Guid.NewGuid():N}@example.test";
        var (uid, _) = await SignUp(email);
        await VerifyEmail(uid);
        var first = await _provider.CreateUserAsync(email, "Mónica Soto", CancellationToken.None);
        var second = await _provider.CreateUserAsync(email, "Mónica Soto", CancellationToken.None);
        Assert.Equal(uid, first);
        Assert.Equal(first, second);
        Assert.Contains("oobCode=", await _provider.CreatePasswordSetupLinkAsync(email, CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unverified_self_registered_account_is_replaced_so_its_password_stops_working()
    {
        if (EmulatorHost is null)
            return;
        var email = $"{Guid.NewGuid():N}@example.test";
        var (strangerUid, strangerToken) = await SignUp(email);

        var invited = await _provider.CreateUserAsync(email, "Mónica Soto", CancellationToken.None);

        Assert.NotEqual(strangerUid, invited);
        // The stranger's session and password no longer reach the invited account.
        Assert.True((await _provider.VerifyAsync(strangerToken, CancellationToken.None))?.Uid != invited);
        using var http = new HttpClient();
        var signIn = await http.PostAsJsonAsync(
            new Uri($"http://{EmulatorHost}/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=fake-api-key"),
            new { email, password = "secret-123456", returnSecureToken = true });
        Assert.False(signIn.IsSuccessStatusCode);
        // Same call again: the account is ours now and still unverified, so it is replaced again, never shared.
        Assert.NotEqual(strangerUid, await _provider.CreateUserAsync(email, "Mónica Soto", CancellationToken.None));
    }

    [Fact]
    public async Task Completing_the_password_setup_link_verifies_the_email()
    {
        if (EmulatorHost is null)
            return;
        var email = $"{Guid.NewGuid():N}@example.test";
        await _provider.CreateUserAsync(email, "Mónica Soto", CancellationToken.None);
        var link = await _provider.CreatePasswordSetupLinkAsync(email, CancellationToken.None);
        var oobCode = System.Web.HttpUtility.ParseQueryString(new Uri(link).Query)["oobCode"];

        using var http = new HttpClient();
        (await http.PostAsJsonAsync(
            new Uri($"http://{EmulatorHost}/identitytoolkit.googleapis.com/v1/accounts:resetPassword?key=fake-api-key"),
            new { oobCode, newPassword = "secret-123456" })).EnsureSuccessStatusCode();
        var signIn = await http.PostAsJsonAsync(
            new Uri($"http://{EmulatorHost}/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=fake-api-key"),
            new { email, password = "secret-123456", returnSecureToken = true });
        signIn.EnsureSuccessStatusCode();
        var token = (await signIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idToken").GetString()!;

        Assert.True((await _provider.VerifyAsync(token, CancellationToken.None))!.EmailVerified);
    }

    [Fact]
    public async Task The_email_of_a_pending_invitation_can_be_changed()
    {
        if (EmulatorHost is null)
            return;
        var uid = await _provider.CreateUserAsync($"{Guid.NewGuid():N}@example.test", "Belem González", CancellationToken.None);
        var corrected = $"{Guid.NewGuid():N}@example.test";

        await _provider.UpdateEmailAsync(uid, corrected, CancellationToken.None);
        // An unverified account is replaced on the next invite; verify it so the lookup by the new address finds this uid.
        await VerifyEmail(uid);

        Assert.Equal(uid, await _provider.CreateUserAsync(corrected, "Belem González", CancellationToken.None));
    }

    private static async Task VerifyEmail(string uid)
    {
        using var http = new HttpClient();
        // Emulator-only admin endpoint (the project's service-account bearer is "owner").
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri($"http://{EmulatorHost}/identitytoolkit.googleapis.com/v1/projects/{ProjectId}/accounts:update"))
        {
            Content = JsonContent.Create(new { localId = uid, emailVerified = true }),
        };
        request.Headers.Authorization = new("Bearer", "owner");
        (await http.SendAsync(request)).EnsureSuccessStatusCode();
    }

    private static async Task<(string Uid, string IdToken)> SignUp(string email)
    {
        using var http = new HttpClient();
        var response = await http.PostAsJsonAsync(
            new Uri($"http://{EmulatorHost}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=fake-api-key"),
            new { email, password = "secret-123456", returnSecureToken = true });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("localId").GetString()!, body.GetProperty("idToken").GetString()!);
    }
}
