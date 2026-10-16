using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Core.Http;

public static class ProblemResponseExtensions
{
    /// <summary>
    /// Problem-details errors, with an unreadable body (malformed JSON, a number where a decimal string is expected)
    /// answered 400 in every environment: minimal APIs always throw <see cref="BadHttpRequestException"/> and the
    /// exception handler keeps its status code instead of turning it into 500 (D-004).
    /// </summary>
    public static IServiceCollection AddPlatformProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
        services.Configure<ExceptionHandlerOptions>(o => o.StatusCodeSelector = e =>
            e is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError);
        return services;
    }
}
