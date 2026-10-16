using Microsoft.AspNetCore.Routing;
using Platform.Core.Access;
using Platform.Core.Users;

namespace Platform.Payroll.Prepayroll;

/// <summary>Prenómina (SPEC §5.4, PLAN D84): the period grid, its desglose, ajustes and incidencias.</summary>
public static class PrepayrollEndpoints
{
    public const string Tag = "prepayroll";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollPrepayroll);

    public static IEndpointRouteBuilder MapPrepayrollEndpoints(this IEndpointRouteBuilder v1) => v1;
}
