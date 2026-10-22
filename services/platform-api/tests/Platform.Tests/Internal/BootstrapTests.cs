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
    public async Task Normalizes_the_email_like_an_invitation()
    {
        var response = await InternalClient().PostAsJsonAsync("/internal/bootstrap", new { email = "  Edgar@Example.Test ", displayName = " Edgar " });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await WithDb(db => db.Set<User>().SingleAsync());
        Assert.Equal("edgar@example.test", user.Email);
        Assert.Equal("Edgar", user.DisplayName);
    }

    [Fact]
    public async Task Retry_after_a_failed_link_issues_a_fresh_link_for_the_same_email()
    {
        Factory.Auth.LinkFailures = 1;
        var body = new { email = "edgar@example.test", displayName = "Edgar" };
        var failed = await InternalClient().PostAsJsonAsync("/internal/bootstrap", body);
        Assert.False(failed.IsSuccessStatusCode);

        var retry = await InternalClient().PostAsJsonAsync("/internal/bootstrap", new { email = " EDGAR@example.test ", displayName = "Edgar" });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var json = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("https://auth.test/setup", json.GetProperty("passwordSetupLink").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, await WithDb(db => db.Set<User>().CountAsync()));
    }

    [Fact]
    public async Task Rerun_is_refused_for_another_email_or_once_the_administrator_signed_in()
    {
        var body = new { email = "edgar@example.test", displayName = "Edgar" };
        Assert.Equal(HttpStatusCode.OK, (await InternalClient().PostAsJsonAsync("/internal/bootstrap", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await InternalClient().PostAsJsonAsync("/internal/bootstrap", new { email = "otro@example.test", displayName = "Otro" })).StatusCode);

        await WithDb(async db =>
        {
            (await db.Set<User>().SingleAsync()).LastLoginAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Conflict, (await InternalClient().PostAsJsonAsync("/internal/bootstrap", body)).StatusCode);
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
