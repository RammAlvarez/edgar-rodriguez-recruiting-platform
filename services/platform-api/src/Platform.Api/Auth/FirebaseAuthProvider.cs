using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Platform.Core.Access;

namespace Platform.Api.Auth;

/// <summary>Settings from env: <c>FIREBASE_PROJECT_ID</c>, <c>FIREBASE_SERVICE_ACCOUNT_JSON</c> (not needed with the emulator).</summary>
public sealed record FirebaseSettings(string ProjectId, string? ServiceAccountJson);

/// <summary>
/// Firebase Auth adapter — the only server file allowed to reference the vendor SDK (CI neutrality check).
/// With <c>FIREBASE_AUTH_EMULATOR_HOST</c> set, the SDK talks to the local emulator.
/// </summary>
public sealed class FirebaseAuthProvider : IAuthProvider
{
    private readonly Lazy<FirebaseAuth> _auth;

    public FirebaseAuthProvider(FirebaseSettings settings)
    {
        _auth = new Lazy<FirebaseAuth>(() =>
        {
            var name = $"platform-{settings.ProjectId}";
            var app = FirebaseApp.GetInstance(name) ?? FirebaseApp.Create(new AppOptions
            {
                ProjectId = settings.ProjectId,
                Credential = settings.ServiceAccountJson is { Length: > 0 } json
                    ? CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential()
                    // The emulator ignores credentials; any token works.
                    : GoogleCredential.FromAccessToken("emulator"),
            }, name);
            return FirebaseAuth.GetAuth(app);
        });
    }

    public async Task<VerifiedIdentity?> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var decoded = await _auth.Value.VerifyIdTokenAsync(token, cancellationToken);
            decoded.Claims.TryGetValue("email", out var email);
            decoded.Claims.TryGetValue("email_verified", out var verified);
            decoded.Claims.TryGetValue("auth_time", out var authTime);
            return new VerifiedIdentity(decoded.Uid, email as string, verified is true, authTime is null ? null : Convert.ToInt64(authTime, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (FirebaseAuthException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<string> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _auth.Value.GetUserByEmailAsync(email, cancellationToken);
            return existing.Uid;
        }
        catch (FirebaseAuthException e) when (e.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            var created = await _auth.Value.CreateUserAsync(new UserRecordArgs
            {
                Email = email,
                DisplayName = displayName,
                EmailVerified = false,
            }, cancellationToken);
            return created.Uid;
        }
    }

    public Task UpdateEmailAsync(string uid, string email, CancellationToken cancellationToken) =>
        _auth.Value.UpdateUserAsync(new UserRecordArgs { Uid = uid, Email = email }, cancellationToken);

    public Task<string> CreatePasswordSetupLinkAsync(string email, CancellationToken cancellationToken) =>
        _auth.Value.GeneratePasswordResetLinkAsync(email, null, cancellationToken);
}
