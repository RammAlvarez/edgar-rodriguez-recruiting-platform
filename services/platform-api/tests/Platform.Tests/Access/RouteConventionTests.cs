using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Tests.Support;

namespace Platform.Tests.Access;

/// <summary>Every /v1 route declares its permission-matrix row; every mutating /v1 route declares its cache tags.</summary>
[Collection(ApiCollection.Name)]
public sealed class RouteConventionTests(ApiFactory factory)
{
    private IEnumerable<RouteEndpoint> V1Endpoints()
    {
        _ = factory.Server; // build the host
        return factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/v1", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Every_v1_route_has_exactly_one_access_rule()
    {
        var endpoints = V1Endpoints().ToList();
        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.Single(e.Metadata.GetOrderedMetadata<AccessRule>()));
    }

    [Fact]
    public void Every_mutating_v1_route_declares_invalidated_tags()
    {
        var mutating = V1Endpoints().Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
            .Any(m => m is "POST" or "PUT" or "PATCH" or "DELETE") == true);
        Assert.All(mutating, e => Assert.NotEmpty(Assert.IsType<InvalidatesMetadata>(e.Metadata.GetMetadata<InvalidatesMetadata>()).Tags));
    }
}
