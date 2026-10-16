using System.Collections.Concurrent;
using Platform.Core.Access;

namespace Platform.Tests.Support;

/// <summary>Tokens are <c>fake:&lt;authUid&gt;</c>; anything else is rejected. Created users get <c>auth-&lt;email&gt;</c>.</summary>
public sealed class FakeAuthProvider : IAuthProvider
{
    public ConcurrentDictionary<string, string> CreatedUsers { get; } = new();

    public static string TokenFor(string authUid) => $"fake:{authUid}";

    public Task<VerifiedIdentity?> VerifyAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult(token.StartsWith("fake:", StringComparison.Ordinal)
            ? new VerifiedIdentity(token["fake:".Length..], null, true)
            : null);

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
