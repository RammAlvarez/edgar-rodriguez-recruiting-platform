using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Platform.Core.Access;

namespace Platform.Core.Users;

public sealed record GrantDto(PermissionModule Module, Guid? ClientId);

public sealed record ActingForDto(Guid Id, string DisplayName);

/// <summary>What the apps learn about the signed-in user (D36): role, grants and client scope come from the database.</summary>
public sealed record MeDto(
    Guid Id,
    string DisplayName,
    string Email,
    UserRole Role,
    Guid? ClientId,
    bool MfaRequired,
    IReadOnlyList<GrantDto> Grants,
    ActingForDto? ActingFor);

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/me", (RequestContext ctx) =>
            {
                var p = ctx.RequiredPrincipal;
                return TypedResults.Ok(new MeDto(
                    p.UserId, p.DisplayName, p.Email, p.Role, p.ClientId, p.MfaRequired,
                    p.Grants.Select(g => new GrantDto(g.Module, g.ClientId)).ToList(),
                    ctx.ActingFor is { } a ? new ActingForDto(a.UserId, a.DisplayName) : null));
            })
            .WithName("GetMe")
            .WithTags("Users")
            .RequireAccess(AccessRule.AnySignedInUser);
        return v1;
    }
}
