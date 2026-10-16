using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Clients;

/// <summary>SPEC §3.3 / §4.5 / §5.6: the two "Nuevo cliente" entries, contacts and sites, and who may use them (DATA_MODEL §8).</summary>
public sealed class ClientTests(ApiFactory factory) : ApiTest(factory)
{
    private static object NewClient(string name = "ecovia ") => new
    {
        shortName = name,
        legalName = "Ecovía Monterrey SA de CV",
        rfc = "eco010101ab1",
        primaryContact = new { fullName = "Laura Medina", email = "laura@ecovia.mx", phone = "8112345670" },
    };

    [Fact]
    public async Task The_recruiting_entry_marks_uses_recruiting_and_creates_the_primary_contact()
    {
        var admin = await As(UserRole.Administrator);

        var response = await admin.PostAsJsonAsync("/v1/clients", NewClient());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("clients", response.Headers.GetValues("X-Invalidates").Single());
        var client = await BodyOf(response);
        Assert.Equal("ECOVIA", client.GetProperty("shortName").GetString());
        Assert.Equal("ECO010101AB1", client.GetProperty("rfc").GetString());
        Assert.True(client.GetProperty("usesRecruiting").GetBoolean());
        Assert.False(client.GetProperty("usesPayroll").GetBoolean());
        var contact = Assert.Single(client.GetProperty("contacts").EnumerateArray());
        Assert.True(contact.GetProperty("isPrimary").GetBoolean());
    }

    [Fact]
    public async Task The_payroll_entry_needs_the_prenomina_grant_and_marks_uses_payroll()
    {
        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await As(UserRole.Administrator)).PostAsJsonAsync("/v1/payroll/clients", NewClient())).StatusCode);

        var payroll = await As(UserRole.Administrator, PermissionModule.PayrollPrepayroll);
        var client = await BodyOf(await payroll.PostAsJsonAsync("/v1/payroll/clients", NewClient()));
        Assert.True(client.GetProperty("usesPayroll").GetBoolean());
        Assert.False(client.GetProperty("usesRecruiting").GetBoolean());
    }

    [Fact]
    public async Task A_client_scoped_prenomina_grant_lists_only_its_clients_and_cannot_create_clients()
    {
        var (ecovia, heb) = await WithDb(async db => (await db.AddClient("ECOVIA"), await db.AddClient("HEB")));
        var scoped = await WithDb(db => db.AddUser(UserRole.Administrator,
            grants: [new UserPermission { Module = PermissionModule.PayrollPrepayroll, ClientId = ecovia.Uid }]));
        var http = Client(scoped.AuthUid);

        var list = await BodyOf(await http.GetAsync("/v1/payroll/clients"));
        Assert.Equal(["ECOVIA"], list.EnumerateArray().Select(c => c.GetProperty("shortName").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/v1/payroll/clients/{heb.Uid}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync($"/v1/payroll/clients/{ecovia.Uid}")).StatusCode);

        var create = await http.PostAsJsonAsync("/v1/payroll/clients", NewClient("NUEVO"));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(ClientEndpoints.ScopedCannotCreate, await DetailOf(create));
    }

    [Theory]
    [InlineData(UserRole.Recruiter)]
    [InlineData(UserRole.Client)]
    public async Task Recruiters_and_clients_have_no_access_to_clients(UserRole role)
    {
        var http = await As(role);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/v1/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsJsonAsync("/v1/clients", NewClient())).StatusCode);
    }

    [Fact]
    public async Task Short_names_are_unique_and_rfc_has_12_or_13_characters()
    {
        var admin = await AsOwner();
        await admin.PostAsJsonAsync("/v1/clients", NewClient());

        var duplicate = await admin.PostAsJsonAsync("/v1/payroll/clients", NewClient("Ecovia"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ClientEndpoints.ShortNameTaken, await DetailOf(duplicate));

        var badRfc = await admin.PostAsJsonAsync("/v1/clients", new { shortName = "HEB", rfc = "HEB01" });
        Assert.Contains("rfc", (await ErrorsOf(badRfc)).Keys);
    }

    [Fact]
    public async Task One_primary_contact_per_client()
    {
        var admin = await AsOwner();
        var id = (await BodyOf(await admin.PostAsJsonAsync("/v1/clients", NewClient()))).GetProperty("id").GetString();

        var detail = await BodyOf(await admin.PostAsJsonAsync($"/v1/clients/{id}/contacts",
            new { fullName = "Rodrigo Salinas", jobTitle = "Compras", isPrimary = true }));

        Assert.Equal(["Rodrigo Salinas"], detail.GetProperty("contacts").EnumerateArray()
            .Where(c => c.GetProperty("isPrimary").GetBoolean()).Select(c => c.GetProperty("fullName").GetString()));
    }

    [Fact]
    public async Task The_client_zone_is_the_zone_of_its_first_active_site()
    {
        var admin = await AsOwner();
        var id = (await BodyOf(await admin.PostAsJsonAsync("/v1/clients", NewClient()))).GetProperty("id").GetString();
        var zones = (await BodyOf(await admin.GetAsync("/v1/catalogs"))).GetProperty("zones").EnumerateArray()
            .ToDictionary(z => z.GetProperty("nameEs").GetString()!, z => z.GetProperty("id").GetString());

        Assert.Equal(JsonValueKind.Null, Row(await BodyOf(await admin.GetAsync("/v1/clients"))).GetProperty("zone").ValueKind);

        var site = await admin.PostAsJsonAsync($"/v1/clients/{id}/sites",
            new { name = "Estación Centro", zoneId = zones["Monterrey"], postalCode = "64000" });
        Assert.Equal(HttpStatusCode.OK, site.StatusCode);
        await admin.PostAsJsonAsync($"/v1/clients/{id}/sites", new { name = "Estación Norte", zoneId = zones["Apodaca"] });

        Assert.Equal("Monterrey", Row(await BodyOf(await admin.GetAsync("/v1/clients"))).GetProperty("zone").GetString());

        var badZip = await admin.PostAsJsonAsync($"/v1/clients/{id}/sites", new { name = "X", postalCode = "640" });
        Assert.Contains("postalCode", (await ErrorsOf(badZip)).Keys);
        var duplicate = await admin.PostAsJsonAsync($"/v1/clients/{id}/sites", new { name = "Estación Centro" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        static JsonElement Row(JsonElement list) => Assert.Single(list.EnumerateArray());
    }

    [Fact]
    public async Task A_contact_with_portal_access_cannot_be_deleted()
    {
        var admin = await AsOwner();
        var client = await WithDb(db => db.AddClient());
        var user = await WithDb(db => db.AddUser(UserRole.Client, clientId: client.Uid));
        var contact = await WithDb(async db =>
        {
            var c = new ClientContact { ClientId = client.Uid, FullName = "Portal", UserId = user.Uid };
            db.Add(c);
            await db.SaveChangesAsync();
            return c;
        });

        var response = await admin.DeleteAsync($"/v1/clients/{client.Uid}/contacts/{contact.Uid}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
