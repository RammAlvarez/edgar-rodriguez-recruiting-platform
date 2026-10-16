using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Access;
using Platform.Core.Users;

namespace Platform.Tests.Access;

/// <summary>The route filter that enforces a permission-matrix row (DATA_MODEL §8).</summary>
public sealed class AccessRuleTests
{
    private static readonly Guid ClientA = Guid.NewGuid();

    [Fact]
    public async Task Role_outside_the_rule_gets_403_with_the_role_message()
    {
        var result = await Run(AccessRule.Administrator(), Principal(UserRole.Recruiter));
        AssertProblem(result, StatusCodes.Status403Forbidden, AccessMessages.RoleNotAllowed);
    }

    [Fact]
    public async Task Administrator_without_the_grant_gets_403_naming_the_module()
    {
        var result = await Run(AccessRule.Administrator(PermissionModule.RecruitingReports),
            Principal(UserRole.Administrator, new Grant(PermissionModule.Users, null)));
        AssertProblem(result, StatusCodes.Status403Forbidden, "No tienes permiso de «Reportes de reclutamiento». Pídelo al administrador.");
    }

    [Fact]
    public async Task Administrator_with_the_grant_passes()
    {
        var result = await Run(AccessRule.Administrator(PermissionModule.PayrollAttendance),
            Principal(UserRole.Administrator, new Grant(PermissionModule.PayrollAttendance, ClientA)));
        Assert.Equal("next", result);
    }

    [Fact]
    public async Task Missing_principal_gets_401()
    {
        var result = await Run(AccessRule.AnySignedInUser, null);
        AssertProblem(result, StatusCodes.Status401Unauthorized, AccessMessages.NotSignedIn);
    }

    [Fact]
    public void Client_scope_is_null_for_an_all_clients_grant_and_the_set_otherwise()
    {
        var all = Principal(UserRole.Administrator, new Grant(PermissionModule.PayrollPrepayroll, null), new Grant(PermissionModule.PayrollPrepayroll, ClientA));
        Assert.Null(all.ClientScope(PermissionModule.PayrollPrepayroll));

        var scoped = Principal(UserRole.Administrator, new Grant(PermissionModule.PayrollPrepayroll, ClientA));
        Assert.Equal([ClientA], scoped.ClientScope(PermissionModule.PayrollPrepayroll)!);
    }

    [Fact]
    public void New_administrators_get_every_grant_except_reports()
    {
        Assert.DoesNotContain(PermissionModule.RecruitingReports, PermissionModules.DefaultForNewAdministrator);
        Assert.Equal(Enum.GetValues<PermissionModule>().Length - 1, PermissionModules.DefaultForNewAdministrator.Count);
    }

    private static Principal Principal(UserRole role, params Grant[] grants) =>
        new(Guid.NewGuid(), "Test", "t@example.test", role, null, false, grants);

    private static async Task<object?> Run(AccessRule rule, Principal? principal)
    {
        var services = new ServiceCollection().AddScoped<RequestContext>().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services.CreateScope().ServiceProvider };
        http.RequestServices.GetRequiredService<RequestContext>().Principal = principal;
        var invocation = EndpointFilterInvocationContext.Create(http);
        return await new AccessEndpointFilter(rule).InvokeAsync(invocation, _ => ValueTask.FromResult<object?>("next"));
    }

    private static void AssertProblem(object? result, int status, string message)
    {
        var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(status, problem.StatusCode);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result);
        Assert.Equal(message, Assert.IsType<Microsoft.AspNetCore.Mvc.ProblemDetails>(value.Value).Detail);
    }
}
