using Microsoft.EntityFrameworkCore;
using Platform.Core.Access;
using Platform.Core.Users;
using Platform.Payroll.Groups;
using Platform.Payroll.Staff;

namespace Platform.Payroll;

/// <summary>
/// DATA_MODEL §8 for Nómina: an Administrador with a payroll grant sees every client, or only the clients the grant is limited
/// to (<c>payroll_groups.client_id</c>); employees, schedules and attendance follow their group.
/// </summary>
public static class PayrollScope
{
    public static IQueryable<PayrollGroup> Groups(DbContext db, RequestContext ctx, PermissionModule module)
    {
        var q = db.Set<PayrollGroup>().AsQueryable();
        return ctx.RequiredPrincipal.ClientScope(module) is { } scope ? q.Where(g => scope.ToList().Contains(g.ClientId)) : q;
    }

    public static IQueryable<Employee> Employees(DbContext db, RequestContext ctx, PermissionModule module)
    {
        var groups = Groups(db, ctx, module);
        return db.Set<Employee>().Where(e => groups.Any(g => g.Uid == e.PayrollGroupId));
    }

    public static bool InScope(RequestContext ctx, PermissionModule module, Guid clientId) =>
        ctx.RequiredPrincipal.ClientScope(module) is not { } scope || scope.Contains(clientId);

}
