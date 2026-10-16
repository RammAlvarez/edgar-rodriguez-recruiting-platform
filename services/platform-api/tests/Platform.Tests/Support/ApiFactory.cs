using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Users;
using Platform.Infrastructure.Persistence;

namespace Platform.Tests.Support;

/// <summary>The real API host against the test database, with the fake identity provider.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string InternalSecret = "test-internal-secret-0123456789-abcdefghij";

    public FakeAuthProvider Auth { get; } = new();

    public FakeObjectStorage Storage { get; } = new();

    public FakeEmailSender Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DATABASE_URL", TestDatabase.ApiConnectionString);
        builder.UseSetting("INTERNAL_SECRET", InternalSecret);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAuthProvider>(Auth);
            services.AddSingleton<IObjectStorage>(Storage);
            services.AddSingleton<IEmailSender>(Email);
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

    /// <summary>A signed-in client for a new user of that role (Administrador: with exactly <paramref name="grants"/>).</summary>
    protected async Task<HttpClient> As(UserRole role, params PermissionModule[] grants)
    {
        var user = await WithDb(async db =>
        {
            Guid? clientId = role == UserRole.Client ? (await db.AddClient($"CLIENTE-{Guid.NewGuid():N}")).Uid : null;
            return await db.AddUser(role, grants: grants.Select(m => new UserPermission { Module = m }), clientId: clientId);
        });
        return Client(user.AuthUid);
    }

    /// <summary>A new user with exactly these grants and a client signed in as them.</summary>
    protected async Task<(User User, HttpClient Http)> SignedIn(UserRole role, params UserPermission[] grants)
    {
        var user = await WithDb(db => db.AddUser(role, $"Usuario {role}", grants: grants));
        return (user, Client(user.AuthUid));
    }

    /// <summary>
    /// A signed-in client for someone whose provider account has <paramref name="email"/> and no users row: a candidate when a
    /// person with an application has that email (PLAN D77).
    /// </summary>
    protected HttpClient AsEmail(string email, bool verified = true)
    {
        var uid = $"email-{Guid.NewGuid():N}";
        Factory.Auth.Emails[uid] = (email, verified);
        return Client(uid);
    }

    /// <summary>An Administrador holding every grant (the owner).</summary>
    protected Task<HttpClient> AsOwner() => As(UserRole.Administrator, [.. PermissionModules.All]);

    protected static async Task<JsonElement> BodyOf(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>The problem-details <c>detail</c> (403/404/409 message).</summary>
    protected static async Task<string?> DetailOf(HttpResponseMessage response) =>
        (await BodyOf(response)).TryGetProperty("detail", out var d) ? d.GetString() : null;

    /// <summary>The 400 field → messages map.</summary>
    protected static async Task<Dictionary<string, string[]>> ErrorsOf(HttpResponseMessage response) =>
        (await BodyOf(response)).GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
}
