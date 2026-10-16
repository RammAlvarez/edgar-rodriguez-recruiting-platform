using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Audit;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Users;

/// <summary>SPEC §6 Usuarios: invitations, edits, delegated grants (PLAN D67) and the self / last-administrator guards.</summary>
public sealed class UserManagementTests(ApiFactory factory) : ApiTest(factory)
{
    private static UserPermission G(PermissionModule m, Guid? client = null) => new() { Module = m, ClientId = client };

    private static UserPermission[] AllGrants => [.. PermissionModules.All.Select(m => G(m))];

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task Inviting_an_administrator_gives_every_grant_but_reportes_and_emails_a_password_link()
    {
        var (_, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var email = NewEmail();

        var response = await owner.PostAsJsonAsync("/v1/users", new { email = email.ToUpperInvariant(), displayName = "Mónica Soto", role = "administrator" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("users,clients", response.Headers.GetValues("X-Invalidates").Single());
        var result = await BodyOf(response);
        Assert.True(result.GetProperty("invitationSent").GetBoolean());
        var grants = result.GetProperty("user").GetProperty("grants").EnumerateArray().Select(g => g.GetProperty("module").GetString()).ToList();
        Assert.Equal(8, grants.Count);
        Assert.Contains("recruiting", grants);
        Assert.DoesNotContain("recruiting_reports", grants);
        var mail = Assert.Single(Factory.Email.To(email));
        Assert.Contains("https://auth.test/setup?email=", mail.Text, StringComparison.Ordinal);
        Assert.Contains("Administrador", mail.Text, StringComparison.Ordinal);
        Assert.Contains("http://localhost:4300", mail.Text, StringComparison.Ordinal);
        Assert.Equal($"auth-{email}", await WithDb(db => db.Set<User>().Where(u => u.Email == email).Select(u => u.AuthUid).SingleAsync()));
    }

    [Fact]
    public async Task A_cliente_user_is_linked_to_its_client_and_contact_and_gets_the_portal_link()
    {
        var (_, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var client = await WithDb(db => db.AddClient("HEB"));
        var contact = await WithDb(async db =>
        {
            var c = new ClientContact { ClientId = client.Uid, FullName = "Laura Medina", Email = NewEmail() };
            db.Add(c);
            await db.SaveChangesAsync();
            return c;
        });

        var missingClient = await owner.PostAsJsonAsync("/v1/users", new { email = contact.Email, displayName = "Laura", role = "client" });
        Assert.Equal(["Elige el cliente al que pertenece el usuario."], (await ErrorsOf(missingClient))["clientId"]);

        var result = await BodyOf(await owner.PostAsJsonAsync("/v1/users",
            new { email = contact.Email, displayName = "Laura Medina", role = "client", clientId = client.Uid, clientContactId = contact.Uid }));

        Assert.Equal("HEB", result.GetProperty("user").GetProperty("clientName").GetString());
        var userId = result.GetProperty("user").GetProperty("id").GetGuid();
        Assert.Equal(userId, await WithDb(db => db.Set<ClientContact>().Where(c => c.Uid == contact.Uid).Select(c => c.UserId).SingleAsync()));
        Assert.Contains("http://localhost:4302", Assert.Single(Factory.Email.To(contact.Email!)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_users_grant_is_required_and_recruiters_are_refused()
    {
        var (_, payrollOnly) = await SignedIn(UserRole.Administrator, G(PermissionModule.PayrollStaff));
        Assert.Equal(HttpStatusCode.Forbidden, (await payrollOnly.GetAsync("/v1/users")).StatusCode);
        var (_, recruiter) = await SignedIn(UserRole.Recruiter);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.PostAsJsonAsync("/v1/users",
            new { email = NewEmail(), displayName = "X", role = "recruiter" })).StatusCode);
    }

    [Fact]
    public async Task Only_grants_the_caller_holds_can_be_handed_out()
    {
        var ecovia = await WithDb(db => db.AddClient("ECOVIA"));
        var (_, delegated) = await SignedIn(UserRole.Administrator, G(PermissionModule.Users), G(PermissionModule.PayrollPrepayroll, ecovia.Uid));

        // The defaults of a new Administrador include grants this admin does not hold.
        var defaults = await delegated.PostAsJsonAsync("/v1/users", new { email = NewEmail(), displayName = "A", role = "administrator" });
        Assert.Equal(HttpStatusCode.Forbidden, defaults.StatusCode);

        var allClients = await delegated.PostAsJsonAsync("/v1/users", new
        {
            email = NewEmail(),
            displayName = "B",
            role = "administrator",
            grants = new[] { new { module = "payroll_prepayroll", clientId = (Guid?)null } },
        });
        Assert.Equal(UserMessages.CannotGrant(PermissionModule.PayrollPrepayroll), await DetailOf(allClients));

        var scoped = await delegated.PostAsJsonAsync("/v1/users", new
        {
            email = NewEmail(),
            displayName = "C",
            role = "administrator",
            grants = new[] { new { module = "payroll_prepayroll", clientId = (Guid?)ecovia.Uid } },
        });
        Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
    }

    [Theory]
    [InlineData("users")]
    [InlineData("recruiting")]
    [InlineData("recruiting_reports")]
    public async Task Recruiting_users_and_reportes_grants_cannot_be_limited_to_a_client(string module)
    {
        var (_, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var client = await WithDb(db => db.AddClient());
        var response = await owner.PostAsJsonAsync("/v1/users", new
        {
            email = NewEmail(),
            displayName = "D",
            role = "administrator",
            grants = new[] { new { module, clientId = (Guid?)client.Uid } },
        });
        Assert.Contains("grants", (await ErrorsOf(response)).Keys);
    }

    [Fact]
    public async Task An_email_already_registered_is_a_409()
    {
        var (existing, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var response = await owner.PostAsJsonAsync("/v1/users", new { email = existing.Email, displayName = "E", role = "recruiter" });
        Assert.Equal(UserMessages.EmailTaken, await DetailOf(response));
    }

    [Fact]
    public async Task A_failed_email_keeps_the_user_and_says_so()
    {
        var (_, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        Factory.Email.Fail = true;
        try
        {
            var result = await BodyOf(await owner.PostAsJsonAsync("/v1/users", new { email = NewEmail(), displayName = "F", role = "recruiter" }));
            Assert.False(result.GetProperty("invitationSent").GetBoolean());
        }
        finally
        {
            Factory.Email.Fail = false;
        }
    }

    [Fact]
    public async Task The_email_can_change_until_the_invitation_is_accepted()
    {
        var (_, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var invited = (await BodyOf(await owner.PostAsJsonAsync("/v1/users", new { email = NewEmail(), displayName = "G", role = "recruiter" })))
            .GetProperty("user");
        var id = invited.GetProperty("id").GetGuid();
        var corrected = NewEmail();
        object Edit(string email) => new { email, displayName = "G", role = "recruiter", isActive = true, mfaRequired = false };

        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/v1/users/{id}", Edit(corrected))).StatusCode);
        var authUid = await WithDb(db => db.Set<User>().Where(u => u.Uid == id).Select(u => u.AuthUid).SingleAsync());
        Assert.Equal(corrected, Factory.Auth.EmailChanges[authUid]);

        await Client(authUid).PostAsync("/v1/me/sessions", null);
        var locked = await owner.PutAsJsonAsync($"/v1/users/{id}", Edit(NewEmail()));
        Assert.Equal(UserMessages.EmailLocked, await DetailOf(locked));
    }

    [Fact]
    public async Task Nobody_changes_their_own_role_grants_or_active_flag()
    {
        var (me, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var response = await owner.PutAsJsonAsync($"/v1/users/{me.Uid}",
            new { email = me.Email, displayName = me.DisplayName, role = "administrator", isActive = false, mfaRequired = false });
        Assert.Equal(UserMessages.Self, await DetailOf(response));

        var rename = await owner.PutAsJsonAsync($"/v1/users/{me.Uid}",
            new { email = me.Email, displayName = "Edgar R.", role = "administrator", isActive = true, mfaRequired = true });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    [Fact]
    public async Task A_delegated_admin_cannot_touch_the_owner_except_name_and_mfa()
    {
        var (owner, _) = await SignedIn(UserRole.Administrator, AllGrants);
        var (_, delegated) = await SignedIn(UserRole.Administrator, G(PermissionModule.Users));

        var deactivate = await delegated.PutAsJsonAsync($"/v1/users/{owner.Uid}",
            new { email = owner.Email, displayName = owner.DisplayName, role = "administrator", isActive = false, mfaRequired = false });
        Assert.Equal(UserMessages.MorePowerful, await DetailOf(deactivate));

        var mfa = await delegated.PutAsJsonAsync($"/v1/users/{owner.Uid}",
            new { email = owner.Email, displayName = owner.DisplayName, role = "administrator", isActive = true, mfaRequired = true });
        Assert.Equal(HttpStatusCode.OK, mfa.StatusCode);
    }

    [Fact]
    public async Task Deactivated_users_are_refused_and_grants_can_be_removed()
    {
        var (_, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var target = await WithDb(db => db.AddUser(UserRole.Administrator, grants: [G(PermissionModule.PayrollStaff), G(PermissionModule.PayrollIncidents)]));

        var edited = await BodyOf(await owner.PutAsJsonAsync($"/v1/users/{target.Uid}", new
        {
            email = target.Email,
            displayName = target.DisplayName,
            role = "administrator",
            isActive = false,
            mfaRequired = false,
            grants = new[] { new { module = "payroll_staff", clientId = (Guid?)null } },
        }));

        Assert.Equal(["payroll_staff"], edited.GetProperty("grants").EnumerateArray().Select(g => g.GetProperty("module").GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(target.AuthUid).GetAsync("/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Resending_an_invitation_sends_a_new_link()
    {
        var (_, owner) = await SignedIn(UserRole.Administrator, AllGrants);
        var target = await WithDb(db => db.AddUser(UserRole.Recruiter));

        var response = await owner.PostAsync($"/v1/users/{target.Uid}/resend-invitation", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(Factory.Email.To(target.Email));
    }

    [Fact]
    public async Task Starting_a_session_records_the_access_and_accepts_the_invitation()
    {
        var (me, http) = await SignedIn(UserRole.Recruiter);

        var response = await http.PostAsync("/v1/me/sessions", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await WithDb(db => db.Set<User>().SingleAsync(u => u.Uid == me.Uid));
        Assert.NotNull(user.LastLoginAt);
        Assert.Equal(user.LastLoginAt, user.AcceptedAt);
        Assert.True(await WithDb(db => db.Set<AuditEntry>().AnyAsync(e => e.RowId == me.Uid && e.Action == AuditAction.Login)));
    }
}
