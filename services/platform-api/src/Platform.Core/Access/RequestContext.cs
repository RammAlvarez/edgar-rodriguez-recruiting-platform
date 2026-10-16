using Platform.Core.Users;

namespace Platform.Core.Access;

public sealed record Grant(PermissionModule Module, Guid? ClientId);

/// <summary>The signed-in user, loaded from the <c>users</c> row on every request (never from token claims).</summary>
public sealed record Principal(
    Guid UserId,
    string DisplayName,
    string Email,
    UserRole Role,
    Guid? ClientId,
    bool MfaRequired,
    IReadOnlyList<Grant> Grants)
{
    public bool IsAdministrator => Role == UserRole.Administrator;

    public bool HasGrant(PermissionModule module) => IsAdministrator && Grants.Any(g => g.Module == module);

    /// <summary>
    /// Client scope of a grant: <c>null</c> = all clients; otherwise the clients the grant is limited to.
    /// Callers must check <see cref="HasGrant"/> first.
    /// </summary>
    public IReadOnlySet<Guid>? ClientScope(PermissionModule module)
    {
        var grants = Grants.Where(g => g.Module == module).ToList();
        return grants.Any(g => g.ClientId is null) ? null : grants.Select(g => g.ClientId!.Value).ToHashSet();
    }
}

/// <summary>
/// A candidate signed in to the Candidates app (SPEC §2.1, DATA_MODEL §8): no <c>users</c> row; the provider's verified email
/// matched one person who has at least one application (PLAN D77).
/// </summary>
public sealed record CandidatePrincipal(Guid PersonId, string DisplayName, string Email);

/// <summary>The recruiter an Administrador is covering for ("Ver como reclutador", SPEC §4.4).</summary>
public sealed record ActingFor(Guid UserId, string DisplayName);

/// <summary>Per-request state filled by the authentication middleware. Scoped.</summary>
public sealed class RequestContext
{
    public string RequestId { get; set; } = "";
    public Principal? Principal { get; set; }
    public CandidatePrincipal? Candidate { get; set; }
    public ActingFor? ActingFor { get; set; }

    public Principal RequiredPrincipal =>
        Principal ?? throw new InvalidOperationException("No principal: the route is missing an access rule.");

    /// <summary>The user whose recruiter scope applies: the covered recruiter in cover mode, otherwise the caller.</summary>
    public Guid ScopeUserId => ActingFor?.UserId ?? RequiredPrincipal.UserId;
}
