using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Http;

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
        v1.MapGet("/me", (RequestContext ctx) => TypedResults.Ok(Me(ctx)))
            .WithName("GetMe")
            .WithTags("Users")
            .RequireAccess(AccessRule.AnySignedInUser);

        // Called by the apps once per sign-in (and app start): last access, invitation accepted, audit «login».
        v1.MapPost("/me/sessions", async (RequestContext ctx, DbContext db, IClock clock, CancellationToken ct) =>
            {
                var user = await db.Set<User>().SingleAsync(u => u.Uid == ctx.RequiredPrincipal.UserId, ct);
                var now = clock.UtcNow;
                user.LastLoginAt = now;
                user.AcceptedAt ??= now;
                db.Add(new AuditEntry
                {
                    OccurredAt = now,
                    UserId = user.Uid,
                    Action = AuditAction.Login,
                    SchemaName = "platform",
                    TableName = "users",
                    RowId = user.Uid,
                    RequestId = ctx.RequestId,
                });
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(Me(ctx));
            })
            .WithName("StartSession")
            .WithTags("Users")
            .RequireAccess(AccessRule.AnySignedInUser)
            .Invalidates(UserEndpoints.Tag);
        return v1;
    }

    private static MeDto Me(RequestContext ctx)
    {
        var p = ctx.RequiredPrincipal;
        return new MeDto(p.UserId, p.DisplayName, p.Email, p.Role, p.ClientId, p.MfaRequired,
            p.Grants.Select(g => new GrantDto(g.Module, g.ClientId)).ToList(),
            ctx.ActingFor is { } a ? new ActingForDto(a.UserId, a.DisplayName) : null);
    }
}
