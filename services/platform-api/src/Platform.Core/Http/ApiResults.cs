using Microsoft.AspNetCore.Http;

namespace Platform.Core.Http;

/// <summary>Error shapes the api-client understands: 400 → field issues, 401/403/404/409 → one user-facing message in <c>detail</c>.</summary>
public static class ApiResults
{
    public static IResult Unauthorized(string message) =>
        TypedResults.Problem(title: "No autenticado", detail: message, statusCode: StatusCodes.Status401Unauthorized);

    public static IResult Forbidden(string message) =>
        TypedResults.Problem(title: "Sin permiso", detail: message, statusCode: StatusCodes.Status403Forbidden);

    public static IResult NotFound(string message) =>
        TypedResults.Problem(title: "No encontrado", detail: message, statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(string message) =>
        TypedResults.Problem(title: "Conflicto", detail: message, statusCode: StatusCodes.Status409Conflict);

    public static IResult Validation(IDictionary<string, string[]> errors) =>
        TypedResults.ValidationProblem(errors, title: "Revisa los campos marcados");
}
