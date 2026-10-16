using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Platform.Core.Http;

/// <summary>
/// Cache tags a mutating route invalidates (ARCHITECTURE §4, D39). Sent as <c>X-Invalidates</c> on every
/// response of the route, errors included, so the api-client re-fetches the live queries tagged with them.
/// </summary>
public sealed record InvalidatesMetadata(IReadOnlyList<string> Tags);

public static class InvalidatesExtensions
{
    public const string HeaderName = "X-Invalidates";

    public static TBuilder Invalidates<TBuilder>(this TBuilder builder, params string[] tags)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new InvalidatesMetadata(tags));

    /// <summary>Writes the header for the matched endpoint before the response starts, whatever the outcome.</summary>
    public static IApplicationBuilder UseInvalidatesHeader(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // Read the tags now: the exception handler clears the endpoint before it writes the error response.
            if (context.GetEndpoint()?.Metadata.GetMetadata<InvalidatesMetadata>() is { Tags.Count: > 0 } meta)
            {
                var value = string.Join(",", meta.Tags);
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[HeaderName] = value;
                    return Task.CompletedTask;
                });
            }
            await next(context);
        });
}
