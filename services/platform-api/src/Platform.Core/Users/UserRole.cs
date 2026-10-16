namespace Platform.Core.Users;

/// <summary>Postgres enum <c>user_role</c>.</summary>
public enum UserRole
{
    Administrator,
    Recruiter,
    Client,
}

/// <summary>Postgres enum <c>permission_module</c> — delegated grants of an Administrador (SPEC §2.2).</summary>
public enum PermissionModule
{
    RecruitingReports,
    PayrollPrepayroll,
    PayrollAttendance,
    PayrollIncidents,
    PayrollStaff,
    PayrollPaymentFiles,
    PayrollCardRequests,
    Users,
}

public static class PermissionModules
{
    /// <summary>Grants given to a new Administrador: everything except reports, which stay with the owner (SPEC §2.2, D5).</summary>
    public static readonly IReadOnlyList<PermissionModule> DefaultForNewAdministrator =
        Enum.GetValues<PermissionModule>().Where(m => m != PermissionModule.RecruitingReports).ToArray();

    public static readonly IReadOnlyList<PermissionModule> All = Enum.GetValues<PermissionModule>();
}
