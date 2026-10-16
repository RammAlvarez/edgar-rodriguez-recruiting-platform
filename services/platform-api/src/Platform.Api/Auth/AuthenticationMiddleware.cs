using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Users;
using Serilog.Context;

namespace Platform.Api.Auth;

/// <summary>
/// For <c>/v1</c>: bearer token → <see cref="IAuthProvider.VerifyAsync"/> → uid → <c>users</c> row (role, active, grants)
/// → <see cref="RequestContext"/>. Cover mode (<c>X-Acting-For</c>) is accepted only for Administradores (DATA_MODEL §8).
/// Requests without a token continue without a principal; the route's access filter answers 401.
/// </summary>
public sealed class AuthenticationMiddleware(RequestDelegate next)
{
    public const string ActingForHeader = "X-Acting-For";

    public async Task InvokeAsync(HttpContext http, RequestContext ctx, IAuthProvider auth, DbContext db)
    {
        ctx.RequestId = http.TraceIdentifier;
        if (!http.Request.Path.StartsWithSegments("/v1"))
        {
            await next(http);
            return;
        }

        var header = http.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var identity = await auth.VerifyAsync(header["Bearer ".Length..].Trim(), http.RequestAborted);
            if (identity is null)
            {
                await ApiResults.Unauthorized("Tu sesión expiró. Vuelve a iniciar sesión.").ExecuteAsync(http);
                return;
            }

            var user = await db.Set<User>().AsNoTracking()
                .Include(u => u.Permissions)
                .SingleOrDefaultAsync(u => u.AuthUid == identity.Uid, http.RequestAborted);
            if (user is null)
            {
                await ApiResults.Forbidden(AccessMessages.NoAccount).ExecuteAsync(http);
                return;
            }
            if (!user.IsActive)
            {
                await ApiResults.Forbidden(AccessMessages.Inactive).ExecuteAsync(http);
                return;
            }

            ctx.Principal = new Principal(user.Uid, user.DisplayName, user.Email, user.Role, user.ClientId, user.MfaRequired,
                user.Role == UserRole.Administrator
                    ? user.Permissions.Select(p => new Grant(p.Module, p.ClientId)).ToList()
                    : []);

            if (http.Request.Headers.TryGetValue(ActingForHeader, out var actingFor) && actingFor.ToString() is { Length: > 0 } raw)
            {
                if (user.Role != UserRole.Administrator)
                {
                    await ApiResults.Forbidden(AccessMessages.CoverModeAdminOnly).ExecuteAsync(http);
                    return;
                }
                var recruiter = Guid.TryParse(raw, out var recruiterId)
                    ? await db.Set<User>().AsNoTracking().SingleOrDefaultAsync(
                        u => u.Uid == recruiterId && u.Role == UserRole.Recruiter && u.IsActive, http.RequestAborted)
                    : null;
                if (recruiter is null)
                {
                    await ApiResults.Forbidden(AccessMessages.CoverModeTargetInvalid).ExecuteAsync(http);
                    return;
                }
                ctx.ActingFor = new ActingFor(recruiter.Uid, recruiter.DisplayName);
            }
        }

        using (LogContext.PushProperty("userId", ctx.Principal?.UserId))
        using (LogContext.PushProperty("actingForUserId", ctx.ActingFor?.UserId))
        {
            await next(http);
        }
    }
}
