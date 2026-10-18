using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Core.Http;

public static class ProblemResponseExtensions
{
    public const string UnreadableTitle = "Solicitud no válida";
    public const string UnreadableDetail = "Un valor no tiene el formato esperado; revisa los campos e inténtalo de nuevo.";

    /// <summary>
    /// Problem-details errors, with an unreadable body (malformed JSON, a number where a decimal string is expected)
    /// answered 400 in every environment: minimal APIs always throw <see cref="BadHttpRequestException"/> and the
    /// exception handler keeps its status code instead of turning it into 500 (D-004). The framework's titles are English
    /// («Bad Request»), so those answers carry a Spanish title and detail the apps can show (D-023).
    /// </summary>
    public static IServiceCollection AddPlatformProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
        {
            if (c.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error is BadHttpRequestException)
            {
                c.ProblemDetails.Title = UnreadableTitle;
                c.ProblemDetails.Detail = UnreadableDetail;
            }
            else if (c.ProblemDetails.Status == StatusCodes.Status500InternalServerError)
            {
                c.ProblemDetails.Title = "Error del servidor";
                c.ProblemDetails.Detail ??= "Ocurrió un error inesperado. Inténtalo de nuevo.";
            }
        });
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
        services.Configure<ExceptionHandlerOptions>(o => o.StatusCodeSelector = e =>
            e is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError);
        return services;
    }
}
