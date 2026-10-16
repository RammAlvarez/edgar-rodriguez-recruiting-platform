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
        var first = await _provider.CreateUserAsync(email, "Mónica Soto", CancellationToken.None);
        var second = await _provider.CreateUserAsync(email, "Mónica Soto", CancellationToken.None);
        Assert.Equal(first, second);
        Assert.Contains("oobCode=", await _provider.CreatePasswordSetupLinkAsync(email, CancellationToken.None), StringComparison.Ordinal);
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
