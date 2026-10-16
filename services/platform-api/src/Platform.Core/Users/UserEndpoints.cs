using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.Persistence;

namespace Platform.Core.Users;

/// <summary>
/// Usuarios (SPEC §6), grant <c>users</c>. An Administrador hands out and removes only grants they hold (PLAN D67), never
/// edits their own role, grants or active flag, and cannot leave the installation without an active all-clients
/// <c>users</c> grant. The invitation email goes out after the commit; a failed send is reported, not rolled back.
/// </summary>
public static partial class UserEndpoints
{
    public const string Tag = "users";

    private static readonly AccessRule Manage = AccessRule.Administrator(PermissionModule.Users);

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/users").WithTags("Users");

        g.MapGet("", async (DbContext db, CancellationToken ct) =>
            {
                var users = await db.Set<User>().AsNoTracking().Include(u => u.Permissions).OrderBy(u => u.DisplayName).ToListAsync(ct);
                var clients = await db.Set<Client>().AsNoTracking().ToDictionaryAsync(c => c.Uid, c => c.ShortName, ct);
                return TypedResults.Ok(users.Select(u => ToDto(u, clients)).ToList());
            })
            .WithName("ListUsers").RequireAccess(Manage);

        g.MapPost("", Invite)
            .WithName("InviteUser").Validate<InviteUserRequest>().Produces<InviteResultDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAccess(Manage).Invalidates(Tag, ClientEndpoints.Tag);

        g.MapPut("/{id:guid}", Update)
            .WithName("UpdateUser").Validate<UpdateUserRequest>().Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).RequireAccess(Manage).Invalidates(Tag, "me");

        g.MapPost("/{id:guid}/resend-invitation", async (Guid id, DbContext db, RequestContext ctx, IAuthProvider auth,
                IEmailSender email, AppLinks links, IClock clock, ILoggerFactory logs, CancellationToken ct) =>
            {
                if (await db.Set<User>().Include(u => u.Permissions).SingleOrDefaultAsync(u => u.Uid == id, ct) is not { } user)
                    return ApiResults.NotFound(UserMessages.NotFound);
                if (!user.IsActive)
                    return ApiResults.Conflict(UserMessages.Inactive);
                user.InvitedAt = clock.UtcNow;
                await db.SaveChangesAsync(ct);
                var sent = await SendInvitation(user, ctx, auth, email, links, logs, ct);
                var clients = await db.Set<Client>().AsNoTracking().ToDictionaryAsync(c => c.Uid, c => c.ShortName, ct);
                return TypedResults.Ok(new InviteResultDto(ToDto(user, clients), sent));
            })
            .WithName("ResendInvitation").Produces<InviteResultDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).RequireAccess(Manage).Invalidates(Tag);

        return v1;
    }

    private static async Task<IResult> Invite(InviteUserRequest r, DbContext db, RequestContext ctx, IAuthProvider auth,
        IEmailSender email, AppLinks links, IClock clock, ILoggerFactory logs, CancellationToken ct)
    {
        var caller = ctx.RequiredPrincipal;
        var grants = r.Role != UserRole.Administrator ? []
            : (r.Grants ?? PermissionModules.DefaultForNewAdministrator.Select(m => new GrantDto(m, null)).ToList()).Distinct().ToList();
        if (GrantRules.FirstUncovered(caller.Grants, [], grants) is { } module)
            return ApiResults.Forbidden(UserMessages.CannotGrant(module));
        if (await ClientProblems(db, r.ClientId, grants, r.ClientContactId, ct) is { } problem)
            return problem;
        var address = r.Email.Trim().ToLowerInvariant();
        if (await db.Set<User>().AnyAsync(u => u.Email == address, ct))
            return ApiResults.Conflict(UserMessages.EmailTaken);

        var authUid = await auth.CreateUserAsync(address, r.DisplayName.Trim(), ct);
        if (await db.Set<User>().AnyAsync(u => u.AuthUid == authUid, ct))
            return ApiResults.Conflict(UserMessages.EmailTaken);
        var user = new User
        {
            AuthUid = authUid,
            Email = address,
            DisplayName = r.DisplayName.Trim(),
            Role = r.Role,
            ClientId = r.Role == UserRole.Client ? r.ClientId : null,
            InvitedAt = clock.UtcNow,
            Permissions = grants.Select(x => new UserPermission { Module = x.Module, ClientId = x.ClientId }).ToList(),
        };
        db.Add(user);
        if (r.ClientContactId is { } contactId)
            (await db.Set<ClientContact>().SingleAsync(c => c.Uid == contactId, ct)).UserId = user.Uid;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.UniqueViolation(e) is not null)
        {
            return ApiResults.Conflict(UserMessages.EmailTaken);
        }

        var sent = await SendInvitation(user, ctx, auth, email, links, logs, ct);
        var clients = await db.Set<Client>().AsNoTracking().ToDictionaryAsync(c => c.Uid, c => c.ShortName, ct);
        return TypedResults.Ok(new InviteResultDto(ToDto(user, clients), sent));
    }

    private static async Task<IResult> Update(Guid id, UpdateUserRequest r, DbContext db, RequestContext ctx, IAuthProvider auth,
        CancellationToken ct)
    {
        var caller = ctx.RequiredPrincipal;
        if (await db.Set<User>().Include(u => u.Permissions).SingleOrDefaultAsync(u => u.Uid == id, ct) is not { } user)
            return ApiResults.NotFound(UserMessages.NotFound);

        var before = user.Permissions.Select(p => new GrantDto(p.Module, p.ClientId)).ToHashSet();
        var after = r.Role != UserRole.Administrator ? [] : (r.Grants ?? [.. before]).ToHashSet();
        var address = r.Email.Trim().ToLowerInvariant();
        var powerChange = user.Role != r.Role || user.IsActive != r.IsActive || user.ClientId != r.ClientId
            || !before.SetEquals(after) || user.Email != address;

        if (powerChange && user.Uid == caller.UserId && (user.Role != r.Role || !r.IsActive || !before.SetEquals(after)))
            return ApiResults.Conflict(UserMessages.Self);
        // Someone holding grants the caller lacks (the owner's Reportes) can only be renamed or asked for MFA.
        if (powerChange && user.Uid != caller.UserId && before.Any(g => !GrantRules.Covers(caller.Grants, g.Module, g.ClientId)))
            return ApiResults.Forbidden(UserMessages.MorePowerful);
        if (GrantRules.FirstUncovered(caller.Grants, before, after) is { } module)
            return ApiResults.Forbidden(UserMessages.CannotGrant(module));
        if (await ClientProblems(db, r.ClientId, after, null, ct) is { } problem)
            return problem;
        if (user.Email != address)
        {
            if (user.AcceptedAt is not null)
                return ApiResults.Conflict(UserMessages.EmailLocked);
            if (await db.Set<User>().AnyAsync(u => u.Email == address && u.Uid != user.Uid, ct))
                return ApiResults.Conflict(UserMessages.EmailTaken);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        user.DisplayName = r.DisplayName.Trim();
        user.Role = r.Role;
        user.ClientId = r.Role == UserRole.Client ? r.ClientId : null;
        user.IsActive = r.IsActive;
        user.MfaRequired = r.MfaRequired;
        user.Permissions.RemoveAll(p => !after.Contains(new GrantDto(p.Module, p.ClientId)));
        foreach (var g in after.Where(g => !before.Contains(g)))
            user.Permissions.Add(new UserPermission { UserId = user.Uid, Module = g.Module, ClientId = g.ClientId });
        var emailChanged = user.Email != address;
        user.Email = address;
        await db.SaveChangesAsync(ct);

        // Backstop: with the self rule and all-clients-only Usuarios grants the caller always still counts, but rows
        // edited outside the API (or a future rule change) must not lock everyone out of Usuarios.
        var usersAdmins = await db.Set<User>().CountAsync(u => u.IsActive && u.Role == UserRole.Administrator
            && u.Permissions.Any(p => p.Module == PermissionModule.Users && p.ClientId == null), ct);
        if (usersAdmins == 0)
            return ApiResults.Conflict(UserMessages.LastUsersAdmin);

        if (emailChanged)
            await auth.UpdateEmailAsync(user.AuthUid, address, ct);
        await tx.CommitAsync(ct);
        var clients = await db.Set<Client>().AsNoTracking().ToDictionaryAsync(c => c.Uid, c => c.ShortName, ct);
        return TypedResults.Ok(ToDto(user, clients));
    }

    /// <summary>The client of a Cliente user, the clients of scoped grants and the invited contact must exist and agree.</summary>
    private static async Task<IResult?> ClientProblems(DbContext db, Guid? clientId, IEnumerable<GrantDto> grants, Guid? contactId,
        CancellationToken ct)
    {
        static IResult Field(string field, string message) =>
            ApiResults.Validation(new Dictionary<string, string[]> { [field] = [message] });

        var ids = grants.Where(g => g.ClientId is not null).Select(g => g.ClientId!.Value)
            .Concat(clientId is { } c ? [c] : []).Distinct().ToList();
        if (await db.Set<Client>().CountAsync(x => ids.Contains(x.Uid), ct) != ids.Count)
            return Field(clientId is not null ? "clientId" : "grants", "El cliente no existe.");
        if (contactId is { } contact)
        {
            var row = await db.Set<ClientContact>().AsNoTracking().SingleOrDefaultAsync(x => x.Uid == contact, ct);
            if (row is null || row.ClientId != clientId)
                return Field("clientContactId", "El contacto no pertenece a ese cliente.");
            if (row.UserId is not null)
                return ApiResults.Conflict("Este contacto ya tiene acceso al portal.");
        }
        return null;
    }

    private static async Task<bool> SendInvitation(User user, RequestContext ctx, IAuthProvider auth, IEmailSender email,
        AppLinks links, ILoggerFactory logs, CancellationToken ct)
    {
        try
        {
            var link = user.Role == UserRole.Client ? links.Clients : await auth.CreatePasswordSetupLinkAsync(user.Email, ct);
            await email.SendAsync(InvitationEmail.Build(user, ctx.RequiredPrincipal.DisplayName, link), ct);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            InvitationFailed(logs.CreateLogger(typeof(UserEndpoints)), e, user.Uid);
            return false;
        }
    }

    public static UserDto ToDto(User u, IReadOnlyDictionary<Guid, string> clients) => new(u.Uid, u.DisplayName, u.Email, u.Role,
        u.ClientId, u.ClientId is { } c ? clients.GetValueOrDefault(c) : null, u.MfaRequired, u.IsActive, u.LastLoginAt,
        u.InvitedAt, u.AcceptedAt, u.Permissions.OrderBy(p => p.Module).Select(p => new GrantDto(p.Module, p.ClientId)).ToList());

    [LoggerMessage(Level = LogLevel.Error, Message = "Invitation email to user {UserId} failed")]
    private static partial void InvitationFailed(ILogger logger, Exception exception, Guid userId);
}
