using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Access;
using Platform.Infrastructure.Persistence;

namespace Platform.Tests.Support;

/// <summary>The real API host against the test database, with the fake identity provider.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string InternalSecret = "test-internal-secret-0123456789-abcdefghij";

    public FakeAuthProvider Auth { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DATABASE_URL", TestDatabase.ApiConnectionString);
        builder.UseSetting("INTERNAL_SECRET", InternalSecret);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAuthProvider>(Auth);
        });
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>();
        PersistenceServices.Configure(options, PersistenceServices.BuildDataSource(TestDatabase.OwnerConnectionString));
        await using var db = new PlatformDbContext(options.Options);
        await db.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

/// <summary>Base for database tests: truncates before each test and gives a scoped DbContext and HTTP helpers.</summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTest(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => TestDatabase.TruncateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected HttpClient Client(string? authUid = null, string? actingFor = null)
    {
        var client = Factory.CreateClient();
        if (authUid is not null)
            client.DefaultRequestHeaders.Authorization = new("Bearer", FakeAuthProvider.TokenFor(authUid));
        if (actingFor is not null)
            client.DefaultRequestHeaders.Add("X-Acting-For", actingFor);
        return client;
    }

    protected async Task<T> WithDb<T>(Func<PlatformDbContext, Task<T>> work)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    protected Task WithDb(Func<PlatformDbContext, Task> work) => WithDb(async db =>
    {
        await work(db);
        return true;
    });
}
