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

/// <summary>Who is signed in: a <c>users</c> role, or <see cref="Candidate"/> — a person with no users row (PLAN D77).</summary>
public enum MeRole
{
    Administrator,
    Recruiter,
    Client,
    Candidate,
}

/// <summary>
/// What the apps learn about the signed-in user (D36): role, grants and client scope come from the database. For a candidate
/// <see cref="Id"/> and <see cref="PersonId"/> are the person's id.
/// </summary>
public sealed record MeDto(
    Guid Id,
    string DisplayName,
    string Email,
    MeRole Role,
    Guid? ClientId,
    bool MfaRequired,
    IReadOnlyList<GrantDto> Grants,
    ActingForDto? ActingFor,
    Guid? PersonId);

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
                var now = clock.UtcNow;
                if (ctx.Candidate is { } candidate)
                {
                    // No users row to stamp: the login is recorded against the person (D77).
                    db.Add(new AuditEntry
                    {
                        OccurredAt = now,
                        Action = AuditAction.Login,
                        SchemaName = "platform",
                        TableName = "people",
                        RowId = candidate.PersonId,
                        RequestId = ctx.RequestId,
                    });
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(Me(ctx));
                }
                var user = await db.Set<User>().SingleAsync(u => u.Uid == ctx.RequiredPrincipal.UserId, ct);
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
        if (ctx.Candidate is { } c)
            return new MeDto(c.PersonId, c.DisplayName, c.Email, MeRole.Candidate, null, false, [], null, c.PersonId);
        var p = ctx.RequiredPrincipal;
        var role = p.Role switch
        {
            UserRole.Administrator => MeRole.Administrator,
            UserRole.Recruiter => MeRole.Recruiter,
            _ => MeRole.Client,
        };
        return new MeDto(p.UserId, p.DisplayName, p.Email, role, p.ClientId, p.MfaRequired,
            p.Grants.Select(g => new GrantDto(g.Module, g.ClientId)).ToList(),
            ctx.ActingFor is { } a ? new ActingForDto(a.UserId, a.DisplayName) : null, null);
    }
}
