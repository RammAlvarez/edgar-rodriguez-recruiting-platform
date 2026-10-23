using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;
using Serilog.Context;

namespace Platform.Api.Auth;

/// <summary>
/// For <c>/v1</c>: bearer token → <see cref="IAuthProvider.VerifyAsync"/> → uid → <c>users</c> row (role, active, grants)
/// → <see cref="RequestContext"/>. Cover mode (<c>X-Acting-For</c>) is accepted only for Administradores (DATA_MODEL §8).
/// A token with no users row is a candidate when its <em>verified</em> email belongs to exactly one person with an
/// application (PLAN D77). Requests without a token continue without a principal; the route's access filter answers 401.
/// </summary>
public sealed class AuthenticationMiddleware(RequestDelegate next)
{
    public const string ActingForHeader = "X-Acting-For";

    /// <summary>What a user pending the MFA code may still call: who they are, the session start, and the MFA routes.</summary>
    private static bool MfaExempt(HttpRequest request) =>
        request.Path.Equals("/v1/me", StringComparison.OrdinalIgnoreCase)
        || request.Path.Equals("/v1/me/sessions", StringComparison.OrdinalIgnoreCase)
        || request.Path.StartsWithSegments("/v1/me/mfa", StringComparison.OrdinalIgnoreCase);

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
                var problem = await ResolveCandidateAsync(ctx, identity, db, http.RequestAborted);
                if (problem is not null)
                {
                    await problem.ExecuteAsync(http);
                    return;
                }
                using (LogContext.PushProperty("personId", ctx.Candidate!.PersonId))
                    await next(http);
                return;
            }
            if (!user.IsActive)
            {
                await ApiResults.Forbidden(AccessMessages.Inactive).ExecuteAsync(http);
                return;
            }

            // Users are authorised by uid alone, so the provider must vouch for the mailbox: an account somebody self-registered
            // with the invited address stays unverified until its owner uses the password-setup link (staff) or signs in with the
            // email link (Cliente users), both of which verify it.
            if (!identity.EmailVerified)
            {
                await ApiResults.Forbidden(AccessMessages.EmailNotVerified).ExecuteAsync(http);
                return;
            }

            ctx.AuthTime = identity.AuthTime;
            var mfaEnabled = user.MfaEnabledAt is not null;
            var mfaVerified = mfaEnabled && identity.AuthTime is { } signedInAt
                && await db.Set<MfaSession>().AnyAsync(s => s.UserId == user.Uid && s.AuthTime == signedInAt, http.RequestAborted);
            ctx.Principal = new Principal(user.Uid, user.DisplayName, user.Email, user.Role, user.ClientId, user.MfaRequired,
                user.Role == UserRole.Administrator
                    ? user.Permissions.Select(p => new Grant(p.Module, p.ClientId)).ToList()
                    : [],
                mfaEnabled, mfaVerified);

            // Verificación en dos pasos (D89): staff who have it, or must, pass the code before anything but /me and /me/mfa.
            if (user.Role is UserRole.Administrator or UserRole.Recruiter && (mfaEnabled || user.MfaRequired) && !mfaVerified
                && !MfaExempt(http.Request))
            {
                await MfaEndpoints.Problem(enrolled: mfaEnabled).ExecuteAsync(http);
                return;
            }

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

    private static async Task<IResult?> ResolveCandidateAsync(RequestContext ctx, VerifiedIdentity identity, DbContext db,
        CancellationToken ct)
    {
        if (!identity.EmailVerified || identity.Email is not { Length: > 0 } email)
            return ApiResults.Forbidden(AccessMessages.NoAccount);
        var address = email.Trim().ToLowerInvariant();
        var people = await db.Set<Person>().AsNoTracking()
            .Where(p => p.Email == address && p.DeletedAt == null && db.Set<Candidate>().Any(c => c.PersonId == p.Uid))
            .Select(p => new { p.Uid, p.FirstNames, p.LastNamePaternal }).Take(2).ToListAsync(ct);
        if (people.Count == 0)
            return ApiResults.Forbidden(AccessMessages.NoAccount);
        if (people.Count > 1)
            return ApiResults.Forbidden(AccessMessages.CandidateEmailShared);
        var p = people[0];
        ctx.Candidate = new CandidatePrincipal(p.Uid, PersonText.Display($"{p.FirstNames} {p.LastNamePaternal}"), address);
        return null;
    }
}
