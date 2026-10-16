using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Internal;

/// <summary>scripts/bootstrap → POST /internal/bootstrap: the first administrator, once, behind INTERNAL_SECRET.</summary>
public sealed class BootstrapTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient InternalClient(string secret = ApiFactory.InternalSecret)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Secret", secret);
        return client;
    }

    [Fact]
    public async Task Wrong_secret_is_rejected()
    {
        var response = await InternalClient("wrong").PostAsJsonAsync("/internal/bootstrap", new { email = "edgar@example.test", displayName = "Edgar" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await WithDb(db => db.Set<User>().CountAsync()));
    }

    [Fact]
    public async Task Creates_the_owner_with_every_grant_and_returns_a_password_link()
    {
        var response = await InternalClient().PostAsJsonAsync("/internal/bootstrap", new { email = "edgar@example.test", displayName = "Edgar Rodríguez" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("https://auth.test/setup", body.GetProperty("passwordSetupLink").GetString(), StringComparison.Ordinal);

        var user = await WithDb(db => db.Set<User>().Include(u => u.Permissions).SingleAsync());
        Assert.Equal(UserRole.Administrator, user.Role);
        Assert.Equal("auth-edgar@example.test", user.AuthUid);
        Assert.Equal(PermissionModules.All.OrderBy(m => m), user.Permissions.Select(p => p.Module).OrderBy(m => m));
        Assert.All(user.Permissions, p => Assert.Null(p.ClientId));

        var me = await Client(user.AuthUid).GetFromJsonAsync<JsonElement>("/v1/me");
        Assert.Equal("Edgar Rodríguez", me.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Refuses_once_an_administrator_exists()
    {
        await WithDb(db => db.AddUser(UserRole.Administrator));
        var response = await InternalClient().PostAsJsonAsync("/internal/bootstrap", new { email = "otro@example.test", displayName = "Otro" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_body_returns_field_messages()
    {
        var response = await InternalClient().PostAsJsonAsync("/internal/bootstrap", new { email = "no-es-correo", displayName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
    }

    [Fact]
    public async Task Unknown_job_is_404()
    {
        var response = await InternalClient().PostAsync("/internal/jobs/nope", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
