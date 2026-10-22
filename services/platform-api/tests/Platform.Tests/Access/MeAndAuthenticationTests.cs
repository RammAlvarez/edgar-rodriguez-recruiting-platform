using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Core.Access;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Access;

/// <summary>Authentication and the identity rows of DATA_MODEL §8: uid → users row → role, active, grants, scope.</summary>
public sealed class MeAndAuthenticationTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Without_token_returns_401_with_message()
    {
        var response = await Client().GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AccessMessages.NotSignedIn, await Detail(response));
    }

    [Fact]
    public async Task Invalid_token_returns_401()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-token");
        var response = await client.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_token_without_users_row_returns_403()
    {
        var response = await Client("someone-unknown").GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AccessMessages.NoAccount, await Detail(response));
    }

    [Fact]
    public async Task Inactive_user_returns_403()
    {
        var user = await WithDb(db => db.AddUser(UserRole.Recruiter, active: false));
        var response = await Client(user.AuthUid).GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AccessMessages.Inactive, await Detail(response));
    }

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Recruiter)]
    public async Task Staff_whose_provider_email_is_unverified_get_403(UserRole role)
    {
        var user = await WithDb(db => db.AddUser(role));
        Factory.Auth.Emails[user.AuthUid] = (user.Email, false);
        var response = await Client(user.AuthUid).GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AccessMessages.EmailNotVerified, await Detail(response));

        Factory.Auth.Emails[user.AuthUid] = (user.Email, true);
        Assert.Equal(HttpStatusCode.OK, (await Client(user.AuthUid).GetAsync("/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Administrator_sees_role_and_grants_from_the_database()
    {
        var client = await WithDb(db => db.AddClient());
        var admin = await WithDb(db => db.AddUser(UserRole.Administrator, "Edgar", grants:
        [
            new UserPermission { Module = PermissionModule.PayrollAttendance, ClientId = client.Uid },
            new UserPermission { Module = PermissionModule.Users },
        ]));

        var me = await Client(admin.AuthUid).GetFromJsonAsync<JsonElement>("/v1/me");

        Assert.Equal(admin.Uid.ToString(), me.GetProperty("id").GetString());
        Assert.Equal("administrator", me.GetProperty("role").GetString());
        var grants = me.GetProperty("grants").EnumerateArray()
            .Select(g => (g.GetProperty("module").GetString(), g.GetProperty("clientId").ValueKind == JsonValueKind.Null ? null : g.GetProperty("clientId").GetString()))
            .OrderBy(g => g.Item1).ToList();
        Assert.Equal([("payroll_attendance", client.Uid.ToString()), ("users", null)], grants);
        Assert.Equal(JsonValueKind.Null, me.GetProperty("actingFor").ValueKind);
    }

    [Fact]
    public async Task Grants_are_ignored_for_non_administrators()
    {
        var recruiter = await WithDb(db => db.AddUser(UserRole.Recruiter, grants: [new UserPermission { Module = PermissionModule.Users }]));
        var me = await Client(recruiter.AuthUid).GetFromJsonAsync<JsonElement>("/v1/me");
        Assert.Equal("recruiter", me.GetProperty("role").GetString());
        Assert.Equal(0, me.GetProperty("grants").GetArrayLength());
    }

    [Fact]
    public async Task Client_user_carries_its_client_scope()
    {
        var client = await WithDb(db => db.AddClient("HEB"));
        var user = await WithDb(db => db.AddUser(UserRole.Client, clientId: client.Uid));
        var me = await Client(user.AuthUid).GetFromJsonAsync<JsonElement>("/v1/me");
        Assert.Equal("client", me.GetProperty("role").GetString());
        Assert.Equal(client.Uid.ToString(), me.GetProperty("clientId").GetString());
    }

    [Fact]
    public async Task Cover_mode_is_accepted_for_an_administrator_covering_an_active_recruiter()
    {
        var admin = await WithDb(db => db.AddUser(UserRole.Administrator, "Edgar"));
        var recruiter = await WithDb(db => db.AddUser(UserRole.Recruiter, "Mónica Soto"));

        var me = await Client(admin.AuthUid, actingFor: recruiter.Uid.ToString()).GetFromJsonAsync<JsonElement>("/v1/me");

        Assert.Equal(admin.Uid.ToString(), me.GetProperty("id").GetString());
        Assert.Equal("Mónica Soto", me.GetProperty("actingFor").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Cover_mode_is_rejected_for_a_recruiter()
    {
        var recruiter = await WithDb(db => db.AddUser(UserRole.Recruiter));
        var other = await WithDb(db => db.AddUser(UserRole.Recruiter));
        var response = await Client(recruiter.AuthUid, actingFor: other.Uid.ToString()).GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AccessMessages.CoverModeAdminOnly, await Detail(response));
    }

    [Theory]
    [InlineData(UserRole.Administrator, true)]
    [InlineData(UserRole.Recruiter, false)]
    public async Task Cover_mode_target_must_be_an_active_recruiter(UserRole targetRole, bool targetActive)
    {
        var admin = await WithDb(db => db.AddUser(UserRole.Administrator));
        var target = await WithDb(db => db.AddUser(targetRole, active: targetActive));
        var response = await Client(admin.AuthUid, actingFor: target.Uid.ToString()).GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AccessMessages.CoverModeTargetInvalid, await Detail(response));
    }

    private static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
}
