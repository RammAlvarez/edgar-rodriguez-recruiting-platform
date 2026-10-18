using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Recruiting;

/// <summary>
/// The Clients app (SPEC §4.6) and the <c>client</c> row of the permission matrix (DATA_MODEL §8, PLAN D78): own visible
/// vacantes only, candidates anonymized until Apto, never documents, salaries of the vacante, Sexo or Edad.
/// </summary>
public sealed class ClientPortalTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _admin = null!;
    private HttpClient _heb = null!;
    private Client _hebClient = null!;
    private User _monica = null!;
    private Guid _opening;

    private async Task Arrange()
    {
        _hebClient = await WithDb(db => db.AddClient("HEB"));
        _monica = await WithDb(async db =>
        {
            var u = await db.AddUser(UserRole.Recruiter, "Mónica Soto");
            u.Phone = "8110001122";
            await db.SaveChangesAsync();
            return u;
        });
        _admin = await AsOwner();
        _opening = await NewOpening(_hebClient.Uid);
        var hebUser = await WithDb(db => db.AddUser(UserRole.Client, "Laura (HEB)", clientId: _hebClient.Uid));
        _heb = Client(hebUser.AuthUid);
    }

    private async Task<Guid> NewOpening(Guid clientId, string position = "Auxiliar de limpieza", bool visible = true)
    {
        var body = OpeningTests.NewOpening(clientId, _monica.Uid, position);
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["visibleToClient"] = visible;
        return (await BodyOf(await _admin.PostAsJsonAsync("/v1/openings", json))).GetProperty("id").GetGuid();
    }

    private async Task<(Guid Candidate, Guid Person)> Capture(Guid opening, string first = "Juan", string last = "Ramírez")
    {
        var c = await BodyOf(await _admin.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(opening,
            new { firstNames = first, lastNamePaternal = last, lastNameMaternal = "Soto", isForeign = false })));
        return (c.GetProperty("id").GetGuid(), c.GetProperty("person").GetProperty("id").GetGuid());
    }

    private Task<HttpResponseMessage> Move(Guid candidate, string stage, string? reason = null, string? hiredAt = null) =>
        _admin.PostAsJsonAsync($"/v1/candidates/{candidate}/stage", new { stage, reason, hiredAt });

    private async Task<JsonElement> PortalCandidate(Guid candidate) =>
        (await BodyOf(await _heb.GetAsync($"/v1/client-portal/openings/{_opening}"))).GetProperty("candidates").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == candidate);

    [Fact]
    public async Task A_client_sees_only_its_own_vacantes_marked_visible()
    {
        await Arrange();
        var hidden = await NewOpening(_hebClient.Uid, "Cajero", visible: false);
        var ecovia = await WithDb(db => db.AddClient("ECOVIA"));
        var other = await NewOpening(ecovia.Uid, "Validador");

        var cards = (await BodyOf(await _heb.GetAsync("/v1/client-portal/openings"))).EnumerateArray().ToList();

        Assert.Equal(_opening, Assert.Single(cards).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await _heb.GetAsync($"/v1/client-portal/openings/{hidden}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _heb.GetAsync($"/v1/client-portal/openings/{other}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _heb.GetAsync($"/v1/client-portal/openings/{other}/comments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _heb.PostAsJsonAsync($"/v1/client-portal/openings/{other}/comments", new { body = "Hola" })).StatusCode);
    }

    [Fact]
    public async Task The_card_has_progress_and_the_lider_but_never_the_salary_sexo_or_edad()
    {
        await Arrange();
        await Capture(_opening);
        var raw = await (await _heb.GetAsync("/v1/client-portal/openings")).Content.ReadAsStringAsync();
        var card = Assert.Single(JsonDocument.Parse(raw).RootElement.EnumerateArray());

        Assert.Equal("HEB – Auxiliar de limpieza", card.GetProperty("displayName").GetString());
        Assert.Equal("in_progress", card.GetProperty("status").GetString());
        Assert.Equal(3, card.GetProperty("headcountRequested").GetInt32());
        Assert.Equal(0, card.GetProperty("hired").GetInt32());
        Assert.Equal("Mónica Soto", card.GetProperty("leadRecruiterName").GetString());
        Assert.Equal("8110001122", card.GetProperty("leadRecruiterPhone").GetString());
        Assert.Equal(1, card.GetProperty("stageCounts").EnumerateArray().Single(s => s.GetProperty("stage").GetString() == "new")
            .GetProperty("count").GetInt32());
        foreach (var hiddenField in new[] { "monthlySalary", "genderRequirement", "ageRange", "notes", "9500", "25-45" })
            Assert.DoesNotContain(hiddenField, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Candidates_are_initials_until_apto_and_identified_at_apto_and_contratado()
    {
        await Arrange();
        var (candidate, _) = await Capture(_opening);

        var before = await PortalCandidate(candidate);
        Assert.False(before.GetProperty("isIdentified").GetBoolean());
        Assert.Equal("J. R.", before.GetProperty("name").GetString());
        Assert.Equal("facebook", before.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("offeredSalary").ValueKind);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("documentsCompleteness").ValueKind);
        var raw = await (await _heb.GetAsync($"/v1/client-portal/openings/{_opening}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("RAMÍREZ", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("9500", raw, StringComparison.Ordinal);

        await Move(candidate, "interview");
        Assert.Equal("interview", (await PortalCandidate(candidate)).GetProperty("stage").GetString());
        Assert.False((await PortalCandidate(candidate)).GetProperty("isIdentified").GetBoolean());

        var at = DateTimeOffset.UtcNow.AddDays(1);
        await _admin.PostAsJsonAsync($"/v1/candidates/{candidate}/interviews",
            new { scheduledAt = at, interviewerId = _monica.Uid, location = "HEB Centro", outcome = "pending", notes = (string?)null });
        await Move(candidate, "suitable");
        var apto = await PortalCandidate(candidate);
        Assert.True(apto.GetProperty("isIdentified").GetBoolean());
        Assert.Equal("RAMÍREZ SOTO JUAN", apto.GetProperty("name").GetString());
        Assert.Equal("9500.00", apto.GetProperty("offeredSalary").GetString());
        Assert.Equal("0.00", apto.GetProperty("documentsCompleteness").GetString());
        Assert.Equal(at.ToUnixTimeSeconds(), apto.GetProperty("interviewAt").GetDateTimeOffset().ToUnixTimeSeconds());

        await Move(candidate, "hired", hiredAt: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.True((await PortalCandidate(candidate)).GetProperty("isIdentified").GetBoolean());

        // Back out of Apto (e.g. the client rejected them): anonymous again.
        await Move(candidate, "discarded", reason: "El cliente lo rechazó");
        var discarded = await PortalCandidate(candidate);
        Assert.False(discarded.GetProperty("isIdentified").GetBoolean());
        Assert.DoesNotContain("El cliente lo rechazó",
            await (await _heb.GetAsync($"/v1/client-portal/openings/{_opening}")).Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_thread_shows_only_comments_meant_for_the_client_and_its_messages_reach_the_recruiters()
    {
        await Arrange();
        await _admin.PostAsJsonAsync($"/v1/openings/{_opening}/comments", new { body = "Mañana enviamos 2 candidatos", visibleToClient = true });
        await _admin.PostAsJsonAsync($"/v1/openings/{_opening}/comments", new { body = "Interno: el cliente paga tarde", visibleToClient = false });

        var posted = await _heb.PostAsJsonAsync($"/v1/client-portal/openings/{_opening}/comments", new { body = "¿Pueden venir el lunes?" });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var mine = await BodyOf(posted);
        Assert.True(mine.GetProperty("fromClient").GetBoolean());
        Assert.Equal("Laura (HEB)", mine.GetProperty("author").GetString());

        var thread = (await BodyOf(await _heb.GetAsync($"/v1/client-portal/openings/{_opening}/comments"))).EnumerateArray().ToList();
        Assert.Equal(["Mañana enviamos 2 candidatos", "¿Pueden venir el lunes?"], thread.Select(c => c.GetProperty("body").GetString()));
        Assert.False(thread[0].GetProperty("fromClient").GetBoolean());

        var staff = (await BodyOf(await _admin.GetAsync($"/v1/openings/{_opening}/comments"))).EnumerateArray().ToList();
        Assert.Contains(staff, c => c.GetProperty("body").GetString() == "¿Pueden venir el lunes?" && c.GetProperty("visibleToClient").GetBoolean());

        var feed = (await BodyOf(await _heb.GetAsync("/v1/client-portal/comments"))).EnumerateArray().ToList();
        Assert.Equal("¿Pueden venir el lunes?", feed[0].GetProperty("body").GetString());
        Assert.Equal("HEB – Auxiliar de limpieza", feed[0].GetProperty("openingName").GetString());
        Assert.DoesNotContain(feed, c => c.GetProperty("body").GetString()!.StartsWith("Interno", StringComparison.Ordinal));

        Assert.Contains("body", (await ErrorsOf(await _heb.PostAsJsonAsync($"/v1/client-portal/openings/{_opening}/comments", new { body = "  " }))).Keys);
    }

    [Fact]
    public async Task A_comment_written_in_cover_mode_reads_as_the_covered_recruiter_alone()
    {
        await Arrange();
        var edgar = await WithDb(db => db.AddUser(UserRole.Administrator, "Edgar",
            grants: PermissionModules.All.Select(m => new UserPermission { Module = m })));
        var covering = Client(edgar.AuthUid, actingFor: _monica.Uid.ToString());
        var staffView = await BodyOf(await covering.PostAsJsonAsync($"/v1/openings/{_opening}/comments",
            new { body = "Entrevistas el jueves", visibleToClient = true }));
        Assert.Equal("Edgar (cubriendo a Mónica Soto)", staffView.GetProperty("author").GetString());

        var thread = (await BodyOf(await _heb.GetAsync($"/v1/client-portal/openings/{_opening}/comments"))).EnumerateArray().Single();
        Assert.Equal("Mónica Soto", thread.GetProperty("author").GetString());
        var feed = (await BodyOf(await _heb.GetAsync("/v1/client-portal/comments"))).EnumerateArray().Single();
        Assert.Equal("Mónica Soto", feed.GetProperty("author").GetString());
    }

    [Fact]
    public async Task A_client_cannot_reach_staff_document_or_candidate_routes_and_staff_cannot_use_the_portal()
    {
        await Arrange();
        var (candidate, person) = await Capture(_opening);

        foreach (var path in new[] { "/v1/candidates", $"/v1/candidates/{candidate}", $"/v1/openings/{_opening}", $"/v1/people/{person}",
                     $"/v1/people/{person}/documents", "/v1/candidate-portal/profile", "/v1/users" })
            Assert.Equal(HttpStatusCode.Forbidden, (await _heb.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _heb.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId = person, documentTypeId = Guid.NewGuid(), fileName = "x.pdf", contentType = "application/pdf", sizeBytes = 10 })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await _admin.GetAsync("/v1/client-portal/openings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await As(UserRole.Recruiter)).GetAsync("/v1/client-portal/openings")).StatusCode);
    }
}
