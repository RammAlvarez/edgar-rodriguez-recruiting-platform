using FluentValidation;
using Platform.Core.Access;

namespace Platform.Core.Users;

public sealed record UserDto(
    Guid Id,
    string DisplayName,
    string Email,
    UserRole Role,
    Guid? ClientId,
    string? ClientName,
    bool MfaRequired,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? InvitedAt,
    DateTimeOffset? AcceptedAt,
    IReadOnlyList<GrantDto> Grants);

/// <summary><see cref="InvitationSent"/> is false when the user was created but the email could not be sent (retry with «Reenviar invitación»).</summary>
public sealed record InviteResultDto(UserDto User, bool InvitationSent);

/// <summary>
/// «Invitar usuario» (SPEC §6). <see cref="Grants"/> only for Administradores; null = the defaults of a new Administrador
/// (everything except Reportes). <see cref="ClientContactId"/> links the contact invited from a client's Contactos tab.
/// </summary>
public sealed record InviteUserRequest(string Email, string DisplayName, UserRole Role, Guid? ClientId,
    IReadOnlyList<GrantDto>? Grants, Guid? ClientContactId);

/// <summary>«Editar usuario»: same fields plus Activo and Requerir MFA. <see cref="Grants"/> replaces the set (Administradores).</summary>
public sealed record UpdateUserRequest(string Email, string DisplayName, UserRole Role, Guid? ClientId, bool IsActive,
    bool MfaRequired, IReadOnlyList<GrantDto>? Grants);

public static class UserMessages
{
    public const string NotFound = "El usuario no existe.";
    public const string EmailTaken = "Ya existe un usuario con ese correo.";
    public const string EmailLocked = "El usuario ya aceptó la invitación; su correo no se puede cambiar.";
    public const string Self = "No puedes cambiar tu propio rol, tus permisos ni desactivarte; pídelo a otro administrador.";
    public const string LastUsersAdmin = "Debe quedar al menos un Administrador activo con el permiso de Usuarios para todos los clientes.";
    public const string MorePowerful = "Este usuario tiene permisos que tú no tienes; solo puedes cambiar su nombre y el requisito de MFA.";
    public const string Inactive = "El usuario está desactivado; actívalo antes de reenviar la invitación.";

    public static string CannotGrant(PermissionModule module) =>
        $"No puedes otorgar ni quitar «{AccessMessages.Label(module)}» porque tú no tienes ese permiso para esos clientes.";
}

internal static class UserRules
{
    /// <summary>Delegated grants that never take a client scope (PLAN D67).</summary>
    public static readonly IReadOnlySet<PermissionModule> AllClientsOnly =
        new HashSet<PermissionModule> { PermissionModule.Users, PermissionModule.RecruitingReports };

    public static void UserFields<T>(this AbstractValidator<T> v, Func<T, string> email, Func<T, string> name,
        Func<T, UserRole> role, Func<T, Guid?> clientId, Func<T, IReadOnlyList<GrantDto>?> grants)
    {
        v.RuleFor(r => email(r)).NotEmpty().WithMessage("Introduce un correo válido.").EmailAddress()
            .WithMessage("Introduce un correo válido.").MaximumLength(120).OverridePropertyName("email");
        v.RuleFor(r => name(r)).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(120).OverridePropertyName("displayName");
        v.RuleFor(r => role(r)).IsInEnum().OverridePropertyName("role");
        v.RuleFor(r => clientId(r)).NotNull().When(r => role(r) == UserRole.Client)
            .WithMessage("Elige el cliente al que pertenece el usuario.").OverridePropertyName("clientId");
        v.RuleFor(r => clientId(r)).Null().When(r => role(r) != UserRole.Client)
            .WithMessage("Solo un usuario Cliente se vincula a un cliente.").OverridePropertyName("clientId");
        v.RuleFor(r => grants(r)).Must(g => g is null || g.Count == 0).When(r => role(r) != UserRole.Administrator)
            .WithMessage("Solo un Administrador tiene permisos delegados.").OverridePropertyName("grants");
        v.RuleFor(r => grants(r)!).Must(g => g.All(x => x.ClientId is null || !AllClientsOnly.Contains(x.Module)))
            .When(r => grants(r) is not null)
            .WithMessage("Los permisos de Usuarios y de Reportes de reclutamiento aplican a todos los clientes.")
            .OverridePropertyName("grants");
        v.RuleForEach(r => grants(r)).Must(g => Enum.IsDefined(g.Module)).WithMessage("Permiso desconocido.")
            .OverridePropertyName("grants");
    }
}

internal sealed class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator() =>
        this.UserFields(r => r.Email, r => r.DisplayName, r => r.Role, r => r.ClientId, r => r.Grants);
}

internal sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator() =>
        this.UserFields(r => r.Email, r => r.DisplayName, r => r.Role, r => r.ClientId, r => r.Grants);
}

/// <summary>The grant arithmetic behind «you can only hand out what you hold» (PLAN D67).</summary>
public static class GrantRules
{
    public static bool Covers(IEnumerable<Grant> holder, PermissionModule module, Guid? clientId) =>
        holder.Any(g => g.Module == module && (g.ClientId is null || g.ClientId == clientId));

    /// <summary>The first grant in the symmetric difference of <paramref name="before"/> and <paramref name="after"/> the caller does not hold.</summary>
    public static PermissionModule? FirstUncovered(IReadOnlyList<Grant> caller, IEnumerable<GrantDto> before, IEnumerable<GrantDto> after)
    {
        var a = before.ToHashSet();
        var b = after.ToHashSet();
        return a.Except(b).Concat(b.Except(a)).Where(g => !Covers(caller, g.Module, g.ClientId))
            .Select(g => (PermissionModule?)g.Module).FirstOrDefault();
    }
}
