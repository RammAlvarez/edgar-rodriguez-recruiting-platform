using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Recruiting.Openings;
using Platform.Tests.Support;

namespace Platform.Tests.Recruiting;

/// <summary>SPEC §4.1 Vacantes: capture, "Cliente – Puesto" names, the recruiter scope, status rules (PLAN D70) and líder / apoyo (D71).</summary>
public sealed class OpeningTests(ApiFactory factory) : ApiTest(factory)
{
    public static object NewOpening(Guid clientId, Guid? lead = null, string position = "Auxiliar de limpieza", int headcount = 3,
        string? requestedAt = null, string gender = "unspecified") => new
        {
            clientId,
            zoneId = (Guid?)null,
            positionName = position,
            headcountRequested = headcount,
            monthlySalary = "9500.00",
            benefits = new[] { "De ley", "Vales de despensa" },
            schedule = "L-V 8:00 a 17:00",
            paymentBankIds = Array.Empty<Guid>(),
            educationLevelId = (Guid?)null,
            yearsExperience = 1,
            requiredExperience = (string?)null,
            genderRequirement = gender,
            ageRange = "25-45",
            priority = "high",
            leadRecruiterId = lead,
            documentTemplateId = (Guid?)null,
            visibleToClient = true,
            notes = (string?)null,
            requestedAt,
        };

    private Task<User> Recruiter(string name) => WithDb(db => db.AddUser(UserRole.Recruiter, name));

    private static async Task<Guid> IdOf(HttpResponseMessage response) => (await BodyOf(response)).GetProperty("id").GetGuid();

    [Fact]
    public async Task A_new_vacante_is_named_cliente_puesto_has_its_lider_and_marks_the_client_as_recruiting()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await Recruiter("Mónica Soto");
        var admin = await AsOwner();

        var response = await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, monica.Uid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("openings", response.Headers.GetValues("X-Invalidates").Single().Split(','));
        var o = await BodyOf(response);
        Assert.Equal("HEB – AUXILIAR DE LIMPIEZA", o.GetProperty("displayName").GetString());
        Assert.Equal("open", o.GetProperty("status").GetString());
        Assert.Equal("Mónica Soto", o.GetProperty("leadRecruiterName").GetString());
        var lead = Assert.Single(o.GetProperty("recruiters").EnumerateArray());
        Assert.True(lead.GetProperty("isLead").GetBoolean());
        Assert.Equal(7, o.GetProperty("stageCounts").GetArrayLength());
        Assert.True(await WithDb(db => db.Set<Client>().Where(c => c.Uid == heb.Uid).Select(c => c.UsesRecruiting).SingleAsync()));

        var events = await BodyOf(await admin.GetAsync($"/v1/openings/{o.GetProperty("id").GetGuid()}/events"));
        Assert.Equal(["lead_changed", "created"], events.EnumerateArray().Select(e => e.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task Two_open_vacantes_for_the_same_puesto_append_the_solicitud_date_until_one_closes()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var admin = await AsOwner();
        var first = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, requestedAt: "2026-10-02")));
        await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, position: "auxiliar  de limpieza", requestedAt: "2026-10-09"));

        var names = (await BodyOf(await admin.GetAsync("/v1/openings"))).EnumerateArray().Select(o => o.GetProperty("displayName").GetString());
        Assert.Equal(["HEB – AUXILIAR DE LIMPIEZA · 09/10/2026", "HEB – AUXILIAR DE LIMPIEZA · 02/10/2026"], names);

        await admin.PostAsJsonAsync($"/v1/openings/{first}/status", new { status = "cancelled", reason = "El cliente ya no la necesita" });
        var after = (await BodyOf(await admin.GetAsync("/v1/openings?status=open"))).EnumerateArray().Single();
        Assert.Equal("HEB – AUXILIAR DE LIMPIEZA", after.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Days_open_count_from_the_solicitud_date()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var requested = new SystemClock().Today().AddDays(-10);
        var o = await BodyOf(await (await AsOwner()).PostAsJsonAsync("/v1/openings",
            NewOpening(heb.Uid, requestedAt: requested.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Equal(10, o.GetProperty("daysOpen").GetInt32());
    }

    [Fact]
    public async Task Field_rules_answer_400_with_the_field()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var admin = await AsOwner();

        var errors = await ErrorsOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, headcount: 0)));
        Assert.Contains("headcountRequested", errors.Keys);

        var notRecruiter = await WithDb(db => db.AddUser(UserRole.Administrator));
        var lead = await ErrorsOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, notRecruiter.Uid)));
        Assert.Equal([OpeningMessages.NotARecruiter], lead["leadRecruiterId"]);

        var unknown = await ErrorsOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(Guid.NewGuid())));
        Assert.Contains("clientId", unknown.Keys);
    }

    [Fact]
    public async Task An_internal_sexo_requirement_of_masculino_is_stored()
    {
        // D-005 shape: Gender.Male is the enum's value 0 and the column has a default.
        var heb = await WithDb(db => db.AddClient("HEB"));
        var o = await BodyOf(await (await AsOwner()).PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, gender: "male")));
        Assert.Equal("male", o.GetProperty("genderRequirement").GetString());
    }

    [Fact]
    public async Task A_recruiter_sees_only_her_vacantes_and_cannot_create_or_edit_them()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await Recruiter("Mónica Soto");
        var admin = await AsOwner();
        var mine = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, monica.Uid)));
        var other = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, position: "Cajero")));
        var http = Client(monica.AuthUid);

        Assert.Equal([mine], (await BodyOf(await http.GetAsync("/v1/openings"))).EnumerateArray().Select(o => o.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/v1/openings/{other}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.PutAsJsonAsync($"/v1/openings/{mine}", NewOpening(heb.Uid))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/v1/recruiters")).StatusCode);
    }

    [Fact]
    public async Task A_supervisor_without_recruiting_cannot_read_vacantes()
    {
        var supervisor = await As(UserRole.Administrator, PermissionModule.PayrollAttendance);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync("/v1/openings")).StatusCode);
    }

    [Fact]
    public async Task In_cover_mode_the_scope_is_the_recruiters_and_entries_read_admin_cubriendo_a_recruiter()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await Recruiter("Mónica Soto");
        var edgar = await WithDb(db => db.AddUser(UserRole.Administrator, "Edgar",
            grants: PermissionModules.All.Select(m => new UserPermission { Module = m })));
        var admin = Client(edgar.AuthUid);
        var mine = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, monica.Uid)));
        await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, position: "Cajero"));

        var covering = Client(edgar.AuthUid, actingFor: monica.Uid.ToString());
        Assert.Single((await BodyOf(await covering.GetAsync("/v1/openings"))).EnumerateArray());

        var comment = await BodyOf(await covering.PostAsJsonAsync($"/v1/openings/{mine}/comments",
            new { body = "Entrevistas el jueves", visibleToClient = true }));
        Assert.Equal("Edgar (cubriendo a Mónica Soto)", comment.GetProperty("author").GetString());
    }

    [Fact]
    public async Task Pausing_or_cancelling_needs_a_reason_and_filled_is_never_chosen_by_hand()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var admin = await AsOwner();
        var id = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid)));

        Assert.Equal([OpeningMessages.ReasonRequired],
            (await ErrorsOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/status", new { status = "paused", reason = " " })))["reason"]);
        Assert.Contains("status", (await ErrorsOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/status",
            new { status = "filled", reason = (string?)null }))).Keys);

        var paused = await BodyOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/status", new { status = "paused", reason = "Presupuesto" }));
        Assert.Equal("paused", paused.GetProperty("status").GetString());
        var resumed = await BodyOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/status", new { status = "open", reason = (string?)null }));
        Assert.Equal("open", resumed.GetProperty("status").GetString());

        var events = (await BodyOf(await admin.GetAsync($"/v1/openings/{id}/events"))).EnumerateArray().ToList();
        Assert.Equal("Presupuesto", events[1].GetProperty("reason").GetString());
        Assert.Equal("paused", events[1].GetProperty("toStatus").GetString());
    }

    [Fact]
    public async Task A_new_lider_demotes_the_previous_one_to_apoyo_and_removing_the_lider_names_the_next()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await Recruiter("Mónica Soto");
        var pricila = await Recruiter("Pricila de León");
        var admin = await AsOwner();
        var id = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, pricila.Uid)));

        var assigned = await BodyOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/recruiters",
            new { userIds = new[] { monica.Uid }, role = "lead" }));
        Assert.Equal(monica.Uid, assigned.GetProperty("leadRecruiterId").GetGuid());
        var roles = assigned.GetProperty("recruiters").EnumerateArray()
            .ToDictionary(r => r.GetProperty("displayName").GetString()!, r => r.GetProperty("isLead").GetBoolean());
        Assert.Equal(new Dictionary<string, bool> { ["Mónica Soto"] = true, ["Pricila de León"] = false }, roles);

        var noNext = await admin.PostAsJsonAsync($"/v1/openings/{id}/recruiters/{monica.Uid}/remove", new { newLeadId = (Guid?)null });
        Assert.Contains("newLeadId", (await ErrorsOf(noNext)).Keys);

        var removed = await BodyOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/recruiters/{monica.Uid}/remove",
            new { newLeadId = pricila.Uid }));
        Assert.Equal(pricila.Uid, removed.GetProperty("leadRecruiterId").GetGuid());

        var last = await BodyOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/recruiters/{pricila.Uid}/remove",
            new { newLeadId = (Guid?)null }));
        Assert.Equal(JsonValueKind.Null, last.GetProperty("leadRecruiterId").ValueKind);
        Assert.Empty(last.GetProperty("recruiters").EnumerateArray());
    }

    [Fact]
    public async Task Asignar_vacante_from_the_recruiter_adds_several_and_shows_them_in_her_list()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await Recruiter("Mónica Soto");
        var pricila = await Recruiter("Pricila de León");
        var admin = await AsOwner();
        var a = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, pricila.Uid)));
        var b = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, position: "Cajero")));

        var response = await admin.PostAsJsonAsync($"/v1/recruiters/{monica.Uid}/openings", new { openingIds = new[] { a, b }, role = "lead" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await BodyOf(response);
        Assert.Equal(2, detail.GetProperty("recruiter").GetProperty("openingsAssigned").GetInt32());
        Assert.All(detail.GetProperty("assignments").EnumerateArray(), x => Assert.True(x.GetProperty("isLead").GetBoolean()));

        var pricilaDetail = await BodyOf(await admin.GetAsync($"/v1/recruiters/{pricila.Uid}"));
        Assert.False(Assert.Single(pricilaDetail.GetProperty("assignments").EnumerateArray()).GetProperty("isLead").GetBoolean());

        Assert.Equal(2, (await BodyOf(await Client(monica.AuthUid).GetAsync("/v1/openings"))).GetArrayLength());
        var notRecruiter = await WithDb(db => db.AddUser(UserRole.Administrator));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/v1/recruiters/{notRecruiter.Uid}/openings",
            new { openingIds = new[] { a }, role = "support" })).StatusCode);
    }

    [Fact]
    public async Task Duplicar_starts_an_open_copy_requested_today_without_recruiters()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await Recruiter("Mónica Soto");
        var admin = await AsOwner();
        var id = await IdOf(await admin.PostAsJsonAsync("/v1/openings", NewOpening(heb.Uid, monica.Uid, requestedAt: "2026-09-01")));

        var copy = await BodyOf(await admin.PostAsJsonAsync($"/v1/openings/{id}/duplicate", new { }));
        Assert.NotEqual(id, copy.GetProperty("id").GetGuid());
        Assert.Equal(new SystemClock().Today().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), copy.GetProperty("requestedAt").GetString());
        Assert.Empty(copy.GetProperty("recruiters").EnumerateArray());
        Assert.Equal("9500.00", copy.GetProperty("monthlySalary").GetString());
    }
}
