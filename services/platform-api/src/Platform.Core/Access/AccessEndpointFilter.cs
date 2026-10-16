using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Http;

namespace Platform.Core.Access;

/// <summary>Enforces the route's <see cref="AccessRule"/>. 401 without a principal, 403 with the user-facing message.</summary>
public sealed class AccessEndpointFilter(AccessRule rule) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var ctx = context.HttpContext.RequestServices.GetRequiredService<RequestContext>();
        if (ctx.Principal is not { } principal)
            return ApiResults.Unauthorized(AccessMessages.NotSignedIn);
        if (!rule.Roles.Contains(principal.Role))
            return ApiResults.Forbidden(AccessMessages.RoleNotAllowed);
        if (rule.Grant is { } grant && principal.IsAdministrator && !principal.HasGrant(grant))
            return ApiResults.Forbidden(AccessMessages.MissingGrant(grant));
        return await next(context);
    }
}

public static class AccessEndpointExtensions
{
    public static TBuilder RequireAccess<TBuilder>(this TBuilder builder, AccessRule rule)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(rule);
        builder.AddEndpointFilter(new AccessEndpointFilter(rule));
        return builder;
    }
}
