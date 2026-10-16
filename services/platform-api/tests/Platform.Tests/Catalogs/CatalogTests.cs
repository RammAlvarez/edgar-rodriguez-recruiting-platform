using System.Net;
using System.Net.Http.Json;
using Platform.Core.Catalogs;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Catalogs;

/// <summary>SPEC §3.4 catalogs: reference data, who reads and edits them (DATA_MODEL §8), and their server rules.</summary>
public sealed class CatalogTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task A_new_installation_has_the_reference_catalogs()
    {
        var c = await BodyOf(await (await As(UserRole.Recruiter)).GetAsync("/v1/catalogs"));

        Assert.Equal(21, c.GetProperty("banks").GetArrayLength());
        Assert.Equal(13, c.GetProperty("documentTypes").GetArrayLength());
        Assert.Equal(51, c.GetProperty("zones").GetArrayLength());
        var banks = c.GetProperty("banks").EnumerateArray().ToDictionary(b => b.GetProperty("speiCode").GetString()!);
        Assert.Equal("banorte", banks["072"].GetProperty("supportsRegistration").GetString());
        Assert.Equal("banregio", banks["058"].GetProperty("supportsRegistration").GetString());
        Assert.True(banks["127"].GetProperty("supportsMassivePayment").GetBoolean());
        var templates = c.GetProperty("documentTemplates").EnumerateArray().ToList();
        Assert.Equal(["candidate", "employee"], templates.Where(t => t.GetProperty("isDefault").GetBoolean())
            .Select(t => t.GetProperty("scope").GetString()).Order());
        Assert.All(templates, t => Assert.True(t.GetProperty("items").GetArrayLength() >= 9));
    }

    [Fact]
    public async Task Clients_cannot_read_catalogs_and_anonymous_calls_are_refused()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await As(UserRole.Client)).GetAsync("/v1/catalogs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/v1/catalogs")).StatusCode);
    }

    [Fact]
    public async Task Any_administrator_edits_catalogs_and_a_recruiter_cannot()
    {
        var recruiter = await As(UserRole.Recruiter);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await recruiter.PostAsJsonAsync("/v1/catalogs/positions", new { nameEs = "VALIDADOR" })).StatusCode);

        // No delegated grant covers catalogs: an Administrador without grants may edit them.
        var admin = await As(UserRole.Administrator);
        var created = await admin.PostAsJsonAsync("/v1/catalogs/positions", new { nameEs = "  VALIDADOR " });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("catalogs", created.Headers.GetValues("X-Invalidates").Single());
        var id = (await BodyOf(created)).GetProperty("id").GetString();

        var updated = await BodyOf(await admin.PutAsJsonAsync($"/v1/catalogs/positions/{id}", new { nameEs = "VALIDADOR", isActive = false }));
        Assert.Equal("VALIDADOR", updated.GetProperty("nameEs").GetString());
        Assert.False(updated.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Duplicates_are_409_and_unknown_rows_404()
    {
        var admin = await AsOwner();
        var duplicate = await admin.PostAsJsonAsync("/v1/catalogs/zones", new { nameEs = "Monterrey", state = "Nuevo León" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Ya existe una zona con ese nombre en ese estado.", await DetailOf(duplicate));

        var missing = await admin.PutAsJsonAsync($"/v1/catalogs/zones/{Guid.NewGuid()}", new { nameEs = "X", state = "Y" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/v1/catalogs/banks",
            new { name = "Otro", speiCode = "072", supportsMassivePayment = false })).StatusCode);
    }

    [Fact]
    public async Task Field_rules_answer_400_with_the_field()
    {
        var admin = await AsOwner();
        var bank = await admin.PostAsJsonAsync("/v1/catalogs/banks", new { name = "Nuevo", speiCode = "72", supportsMassivePayment = false });
        Assert.Equal(["La clave SPEI tiene 3 dígitos."], (await ErrorsOf(bank))["speiCode"]);

        var type = await admin.PostAsJsonAsync("/v1/catalogs/document-types",
            new { code = "LICENCIA", nameEs = "Licencia", hasExpiry = false, defaultValidityDays = 30, sortOrder = 5 });
        Assert.Equal(["Solo un documento con vencimiento tiene vigencia."], (await ErrorsOf(type))["defaultValidityDays"]);
    }

    [Fact]
    public async Task A_new_default_template_takes_the_flag_from_the_previous_one()
    {
        var admin = await AsOwner();
        var catalogs = await BodyOf(await admin.GetAsync("/v1/catalogs"));
        var ine = catalogs.GetProperty("documentTypes").EnumerateArray().Single(d => d.GetProperty("code").GetString() == "INE");

        var created = await admin.PostAsJsonAsync("/v1/catalogs/document-templates", new
        {
            nameEs = "Candidato — mínimo",
            scope = "candidate",
            isDefault = true,
            items = new[] { new { documentTypeId = ine.GetProperty("id").GetString(), isRequired = true } },
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var defaults = (await BodyOf(await admin.GetAsync("/v1/catalogs"))).GetProperty("documentTemplates").EnumerateArray()
            .Where(t => t.GetProperty("isDefault").GetBoolean()).Select(t => t.GetProperty("nameEs").GetString()).Order();
        Assert.Equal(["Candidato — mínimo", "Empleado — expediente"], defaults);
    }

    [Fact]
    public async Task Template_items_must_be_existing_document_types()
    {
        var response = await (await AsOwner()).PostAsJsonAsync("/v1/catalogs/document-templates", new
        {
            nameEs = "Rota",
            scope = "employee",
            isDefault = false,
            items = new[] { new { documentTypeId = Guid.NewGuid(), isRequired = true } },
        });
        Assert.Equal(["Uno de los documentos no existe."], (await ErrorsOf(response))["items"]);
    }

    [Fact]
    public async Task Statutory_holidays_are_added_once_per_year()
    {
        var admin = await AsOwner();
        var first = await BodyOf(await admin.PostAsJsonAsync("/v1/catalogs/holidays/statutory", new { year = 2026 }));
        Assert.Equal(7, first.GetProperty("added").GetInt32());
        Assert.Equal(["2026-01-01", "2026-02-02", "2026-03-16", "2026-05-01", "2026-09-16", "2026-11-16", "2026-12-25"],
            first.GetProperty("holidays").EnumerateArray().Select(h => h.GetProperty("date").GetString()));

        var again = await BodyOf(await admin.PostAsJsonAsync("/v1/catalogs/holidays/statutory", new { year = 2026 }));
        Assert.Equal(0, again.GetProperty("added").GetInt32());
    }

    [Theory]
    [InlineData(2030, true)]
    [InlineData(2024, true)]
    [InlineData(2027, false)]
    public void The_presidential_transmission_is_a_holiday_every_six_years(int year, bool expected) =>
        Assert.Equal(expected, StatutoryHolidays.For(year).Any(h => h.Date == new DateOnly(year, 10, 1)));
}
