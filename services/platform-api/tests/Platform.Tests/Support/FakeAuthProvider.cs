using System.Collections.Concurrent;
using Platform.Core.Access;

namespace Platform.Tests.Support;

/// <summary>
/// Tokens are <c>fake:&lt;authUid&gt;</c>, signed in at time 1 — or <c>fake:&lt;authUid&gt;@&lt;authTime&gt;</c> for another sign-in;
/// anything else is rejected. Created users get <c>auth-&lt;email&gt;</c>. An identity
/// registered in <see cref="Emails"/> carries that email (how a candidate, who has no users row, is recognised).
/// </summary>
public sealed class FakeAuthProvider : IAuthProvider
{
    public ConcurrentDictionary<string, string> CreatedUsers { get; } = new();

    public ConcurrentDictionary<string, (string Email, bool Verified)> Emails { get; } = new();

    public static string TokenFor(string authUid, long authTime = 1) => authTime == 1 ? $"fake:{authUid}" : $"fake:{authUid}@{authTime}";

    public Task<VerifiedIdentity?> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        if (!token.StartsWith("fake:", StringComparison.Ordinal))
            return Task.FromResult<VerifiedIdentity?>(null);
        var rest = token["fake:".Length..];
        var at = rest.LastIndexOf('@');
        var (uid, authTime) = at > 0 && long.TryParse(rest[(at + 1)..], out var t) ? (rest[..at], t) : (rest, 1L);
        return Task.FromResult<VerifiedIdentity?>(Emails.TryGetValue(uid, out var e)
            ? new VerifiedIdentity(uid, e.Email, e.Verified, authTime)
            : new VerifiedIdentity(uid, null, true, authTime));
    }

    public Task<string> CreateUserAsync(string email, string displayName, CancellationToken cancellationToken) =>
        Task.FromResult(CreatedUsers.GetOrAdd(email, e => $"auth-{e}"));

    public ConcurrentDictionary<string, string> EmailChanges { get; } = new();

    public Task UpdateEmailAsync(string uid, string email, CancellationToken cancellationToken)
    {
        EmailChanges[uid] = email;
        return Task.CompletedTask;
    }

    public Task<string> CreatePasswordSetupLinkAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult($"https://auth.test/setup?email={Uri.EscapeDataString(email)}");
}
