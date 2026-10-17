namespace Platform.Core.Access;

/// <summary>What the identity provider vouches for. Roles are never read from the token (D36).</summary>
/// <summary>
/// A verified token. <paramref name="AuthTime"/> is when that sign-in happened (seconds since 1970): the same for every
/// refreshed token of one sign-in, new on the next — what a verified MFA code is bound to (PLAN D89).
/// </summary>
public sealed record VerifiedIdentity(string Uid, string? Email, bool EmailVerified, long? AuthTime = null);

/// <summary>
/// The server side of the identity provider (ARCHITECTURE §2). The only implementation that talks to a vendor lives in
/// <c>Platform.Api/Auth/Firebase*.cs</c>; tests use a fake.
/// </summary>
public interface IAuthProvider
{
    /// <summary>Verifies a bearer token; <c>null</c> when it is invalid or expired.</summary>
    Task<VerifiedIdentity?> VerifyAsync(string token, CancellationToken cancellationToken);

    /// <summary>Creates the provider account for an invited user (or returns the existing one with that email) and returns its uid.</summary>
    Task<string> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken);

    /// <summary>Changes the sign-in email of an account whose invitation was not accepted yet.</summary>
    Task UpdateEmailAsync(string uid, string email, CancellationToken cancellationToken);

    /// <summary>A one-time link the user opens to set their password (admin app, email/password sign-in).</summary>
    Task<string> CreatePasswordSetupLinkAsync(string email, CancellationToken cancellationToken);
}
