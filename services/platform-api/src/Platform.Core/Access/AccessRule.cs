using Platform.Core.Users;

namespace Platform.Core.Access;

/// <summary>
/// Route metadata from the permission matrix (DATA_MODEL §8): which roles may call the route and, for
/// Administradores, which delegated grant is required; <see cref="AllowsCandidate"/> lets a signed-in candidate (no users row,
/// PLAN D77) through as well. Every <c>/v1</c> route must carry exactly one.
/// </summary>
public sealed record AccessRule(IReadOnlySet<UserRole> Roles, PermissionModule? Grant = null, bool AllowsCandidate = false)
{
    /// <summary>Anyone signed in, candidates included (<c>/v1/me</c>).</summary>
    public static AccessRule AnySignedInUser { get; } =
        new(new HashSet<UserRole>(Enum.GetValues<UserRole>()), AllowsCandidate: true);

    /// <summary>Only a candidate, on their own data (Candidates app).</summary>
    public static AccessRule Candidate { get; } = new(new HashSet<UserRole>(), AllowsCandidate: true);

    public static AccessRule Administrator(PermissionModule? grant = null) =>
        new(new HashSet<UserRole> { UserRole.Administrator }, grant);

    public static AccessRule ForRoles(params UserRole[] roles) => new(roles.ToHashSet());
}

public static class AccessMessages
{
    public const string NotSignedIn = "Inicia sesión para continuar.";
    public const string NoAccount = "Tu cuenta no tiene acceso a la plataforma. Pide una invitación al administrador.";
    public const string CandidateEmailShared =
        "Tu correo está registrado en más de una persona. Pide a tu reclutador que lo corrija para poder entrar.";
    public const string Inactive = "Tu cuenta está desactivada. Contacta al administrador.";
    public const string RoleNotAllowed = "No tienes permiso para esta sección.";
    public const string CoverModeAdminOnly = "Solo un Administrador puede cubrir a un reclutador.";
    public const string CoverModeTargetInvalid = "La persona que intentas cubrir no es un reclutador activo.";

    public static string MissingGrant(PermissionModule module) => $"No tienes permiso de «{Label(module)}». Pídelo al administrador.";

    public static string Label(PermissionModule module) => module switch
    {
        PermissionModule.Recruiting => "Reclutamiento",
        PermissionModule.RecruitingReports => "Reportes de reclutamiento",
        PermissionModule.PayrollPrepayroll => "Prenómina",
        PermissionModule.PayrollAttendance => "Asistencia",
        PermissionModule.PayrollIncidents => "Incidencias",
        PermissionModule.PayrollStaff => "Personal",
        PermissionModule.PayrollPaymentFiles => "Archivos de pago",
        PermissionModule.PayrollCardRequests => "Solicitud de tarjetas",
        PermissionModule.Users => "Usuarios",
        _ => module.ToString(),
    };
}
