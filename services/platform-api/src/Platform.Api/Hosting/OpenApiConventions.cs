using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Platform.Api.Hosting;

/// <summary>Publishes <see cref="decimal"/> as <c>type: string, format: decimal</c>, matching the wire converter (PLAN D46).</summary>
public sealed class DecimalAsStringSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (type == typeof(decimal))
        {
            var nullable = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) is not null;
            schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
            schema.Format = "decimal";
            schema.Pattern = @"^-?\d+(\.\d+)?$";
        }
        return Task.CompletedTask;
    }
}
