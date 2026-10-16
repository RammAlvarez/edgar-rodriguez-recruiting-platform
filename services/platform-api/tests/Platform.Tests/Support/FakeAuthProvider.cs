using System.Collections.Concurrent;
using Platform.Core.Access;

namespace Platform.Tests.Support;

/// <summary>
/// Tokens are <c>fake:&lt;authUid&gt;</c>; anything else is rejected. Created users get <c>auth-&lt;email&gt;</c>. An identity
/// registered in <see cref="Emails"/> carries that email (how a candidate, who has no users row, is recognised).
/// </summary>
public sealed class FakeAuthProvider : IAuthProvider
{
    public ConcurrentDictionary<string, string> CreatedUsers { get; } = new();

    public ConcurrentDictionary<string, (string Email, bool Verified)> Emails { get; } = new();

    public static string TokenFor(string authUid) => $"fake:{authUid}";

    public Task<VerifiedIdentity?> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        if (!token.StartsWith("fake:", StringComparison.Ordinal))
            return Task.FromResult<VerifiedIdentity?>(null);
        var uid = token["fake:".Length..];
        return Task.FromResult<VerifiedIdentity?>(Emails.TryGetValue(uid, out var e)
            ? new VerifiedIdentity(uid, e.Email, e.Verified)
            : new VerifiedIdentity(uid, null, true));
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
