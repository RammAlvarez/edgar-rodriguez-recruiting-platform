using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Audit;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;
using Platform.Tests.People;
using Platform.Tests.Support;

namespace Platform.Tests.Recruiting;

/// <summary>SPEC §4.2–4.3 Candidatos: capture, the stage machine (PLAN D72), the vacante's derived status (D70), scope (D73), notes (D74).</summary>
public sealed class CandidateTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _admin = null!;
    private User _monica = null!;
    private Guid _opening;

    private async Task Arrange(int headcount = 3)
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        _monica = await WithDb(db => db.AddUser(UserRole.Recruiter, "Mónica Soto"));
        _admin = await AsOwner();
        _opening = (await BodyOf(await _admin.PostAsJsonAsync("/v1/openings",
            OpeningTests.NewOpening(heb.Uid, _monica.Uid, headcount: headcount)))).GetProperty("id").GetGuid();
    }

    public static object NewCandidate(Guid opening, object? person = null, Guid? personId = null, string source = "facebook") => new
    {
        personId,
        person = personId is null ? person ?? PeopleTests.NewPerson() : null,
        jobOpeningId = opening,
        source,
        offeredSalary = "9500.00",
        comments = (string?)null,
        capturedAt = (string?)null,
    };

    private static object NewPerson(string first, string? curp = null) => new
    {
        firstNames = first,
        lastNamePaternal = "Garza",
        lastNameMaternal = "Treviño",
        curp,
        isForeign = false,
        mobilePhone = "8187654321",
    };

    private async Task<JsonElement> Capture(HttpClient http, object? person = null) =>
        await BodyOf(await http.PostAsJsonAsync("/v1/candidates", NewCandidate(_opening, person)));

    private static Task<HttpResponseMessage> Move(HttpClient http, Guid candidate, string stage, string? reason = null, string? hiredAt = null) =>
        http.PostAsJsonAsync($"/v1/candidates/{candidate}/stage", new { stage, reason, hiredAt });

    private async Task<JsonElement> Opening() => await BodyOf(await _admin.GetAsync($"/v1/openings/{_opening}"));

    [Fact]
    public async Task Capturing_with_a_new_person_creates_both_assigns_the_lider_and_puts_the_vacante_in_progress()
    {
        await Arrange();

        var response = await _admin.PostAsJsonAsync("/v1/candidates", NewCandidate(_opening));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("candidates", response.Headers.GetValues("X-Invalidates").Single().Split(','));
        var c = await BodyOf(response);
        Assert.Equal("PÉREZ LÓPEZ JUAN CARLOS", c.GetProperty("person").GetProperty("fullName").GetString());
        Assert.Equal("HEB – Auxiliar de limpieza", c.GetProperty("openingName").GetString());
        Assert.Equal("new", c.GetProperty("stage").GetString());
        Assert.Equal("Mónica Soto", c.GetProperty("recruiterName").GetString());
        Assert.False(c.GetProperty("canConvertToEmployee").GetBoolean());
        Assert.Equal("in_progress", (await Opening()).GetProperty("status").GetString());

        var history = Assert.Single((await BodyOf(await _admin.GetAsync($"/v1/candidates/{c.GetProperty("id").GetGuid()}/history")))
            .EnumerateArray());
        Assert.Equal("new", history.GetProperty("toStage").GetString());
        Assert.Equal(JsonValueKind.Null, history.GetProperty("fromStage").ValueKind);
    }

    [Fact]
    public async Task The_same_person_twice_on_one_vacante_or_a_duplicate_curp_is_a_409()
    {
        await Arrange();
        var person = (await Capture(_admin)).GetProperty("person").GetProperty("id").GetGuid();

        var again = await _admin.PostAsJsonAsync("/v1/candidates", NewCandidate(_opening, personId: person));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var duplicate = await _admin.PostAsJsonAsync("/v1/candidates", NewCandidate(_opening, PeopleTests.NewPerson(rfc: null)));
        Assert.Equal("Ya existe una persona con esa CURP: PÉREZ LÓPEZ JUAN CARLOS.", await DetailOf(duplicate));
    }

    [Fact]
    public async Task A_paused_vacante_takes_no_candidates()
    {
        await Arrange();
        await _admin.PostAsJsonAsync($"/v1/openings/{_opening}/status", new { status = "paused", reason = "Presupuesto" });
        var response = await _admin.PostAsJsonAsync("/v1/candidates", NewCandidate(_opening));
        Assert.Equal(CandidateMessages.OpeningClosed(Platform.Recruiting.Openings.OpeningStatus.Paused), await DetailOf(response));
    }

    [Fact]
    public async Task No_apto_and_descartado_need_a_reason_and_contratado_needs_the_fecha_de_ingreso()
    {
        await Arrange();
        var id = (await Capture(_admin)).GetProperty("id").GetGuid();

        Assert.Equal([CandidateMessages.ReasonRequired], (await ErrorsOf(await Move(_admin, id, "not_suitable")))["reason"]);
        Assert.Equal([CandidateMessages.ReasonRequired], (await ErrorsOf(await Move(_admin, id, "discarded", " ")))["reason"]);
        Assert.Equal([CandidateMessages.HiredAtRequired], (await ErrorsOf(await Move(_admin, id, "hired")))["hiredAt"]);
        Assert.Equal([CandidateMessages.HiredBeforeCapture], (await ErrorsOf(await Move(_admin, id, "hired", hiredAt: "2020-01-01")))["hiredAt"]);

        var notSuitable = await BodyOf(await Move(_admin, id, "not_suitable", "No pasó la prueba"));
        Assert.False(notSuitable.GetProperty("candidate").GetProperty("isSuitable").GetBoolean());
        Assert.Equal("No pasó la prueba", notSuitable.GetProperty("candidate").GetProperty("discardReason").GetString());

        var suitable = await BodyOf(await Move(_admin, id, "suitable"));
        Assert.True(suitable.GetProperty("candidate").GetProperty("isSuitable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, suitable.GetProperty("candidate").GetProperty("discardReason").ValueKind);
        Assert.Equal(CandidateMessages.SameStage, await DetailOf(await Move(_admin, id, "suitable")));
    }

    [Fact]
    public async Task Hiring_the_headcount_fills_the_vacante_in_the_same_request_and_undoing_it_reopens_it()
    {
        await Arrange(headcount: 1);
        var first = (await Capture(_admin)).GetProperty("id").GetGuid();
        var second = (await Capture(_admin, NewPerson("Ana"))).GetProperty("id").GetGuid();

        var hiredAt = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); // never before the capture date, which uses the real clock
        var hired = await BodyOf(await Move(_admin, first, "hired", hiredAt: hiredAt));
        Assert.Equal(1, hired.GetProperty("hired").GetInt32());
        Assert.Equal("filled", hired.GetProperty("openingStatus").GetString());
        var opening = await Opening();
        Assert.Equal("filled", opening.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, opening.GetProperty("filledAt").ValueKind);

        Assert.Equal(CandidateMessages.HeadcountReached(1), await DetailOf(await Move(_admin, second, "hired", hiredAt: hiredAt)));

        await Move(_admin, first, "suitable");
        opening = await Opening();
        Assert.Equal("in_progress", opening.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, opening.GetProperty("filledAt").ValueKind);
        var events = (await BodyOf(await _admin.GetAsync($"/v1/openings/{_opening}/events"))).EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "status_changed").Select(e => e.GetProperty("toStatus").GetString());
        Assert.Equal(["in_progress", "filled", "in_progress"], events.Reverse());
    }

    [Fact]
    public async Task A_recruiter_captures_for_herself_and_sees_only_the_people_of_her_vacantes()
    {
        await Arrange();
        var other = (await BodyOf(await _admin.PostAsJsonAsync("/v1/openings",
            OpeningTests.NewOpening((await Opening()).GetProperty("clientId").GetGuid(), position: "Cajero")))).GetProperty("id").GetGuid();
        var foreign = await BodyOf(await _admin.PostAsJsonAsync("/v1/candidates", NewCandidate(other, NewPerson("Ana"))));
        var http = Client(_monica.AuthUid);

        var mine = await BodyOf(await http.PostAsJsonAsync("/v1/candidates", NewCandidate(_opening)));
        Assert.Equal(_monica.Uid, mine.GetProperty("recruiterId").GetGuid());

        Assert.Single((await BodyOf(await http.GetAsync("/v1/candidates"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/v1/candidates/{foreign.GetProperty("id").GetGuid()}")).StatusCode);
        var foreignPerson = foreign.GetProperty("person").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/v1/people/{foreignPerson}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/v1/people/{foreignPerson}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await http.GetAsync($"/v1/people/{mine.GetProperty("person").GetProperty("id").GetGuid()}/documents")).StatusCode);
        // The registry search stays open so «Nuevo candidato» can find a person (D73).
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/v1/people?search=garza")).StatusCode);
    }

    [Fact]
    public async Task Opening_a_candidate_logs_the_profile_read()
    {
        await Arrange();
        var c = await Capture(_admin);
        await _admin.GetAsync($"/v1/candidates/{c.GetProperty("id").GetGuid()}");
        var person = c.GetProperty("person").GetProperty("id").GetGuid();
        Assert.Equal(DataAccessKind.ViewProfile,
            Assert.Single(await WithDb(db => db.Set<DataAccessEntry>().Where(e => e.PersonId == person).ToListAsync())).Access);
    }

    [Fact]
    public async Task Notes_keep_only_the_allowlist_open_links_in_a_new_tab_and_are_edited_by_their_author_or_an_administrator()
    {
        await Arrange();
        var id = (await Capture(_admin)).GetProperty("id").GetGuid();
        var http = Client(_monica.AuthUid);

        var note = await BodyOf(await http.PostAsJsonAsync($"/v1/candidates/{id}/notes", new
        {
            contentHtml = "<h2 style=\"color:red\">Entrevista</h2><p>Llegó <span>puntual</span><script>alert(1)</script></p>"
                + "<p><a href=\"https://linkedin.com/in/x\" onclick=\"x()\">perfil</a> <a href=\"javascript:alert(1)\">mal</a></p><img src=x>",
            contentJson = "{\"type\":\"doc\"}",
        }));
        Assert.Equal("<h2>Entrevista</h2><p>Llegó puntual</p>"
            + "<p><a href=\"https://linkedin.com/in/x\" target=\"_blank\" rel=\"noopener\">perfil</a> <a target=\"_blank\" rel=\"noopener\">mal</a></p>",
            note.GetProperty("contentHtml").GetString());
        Assert.Equal("Mónica Soto", note.GetProperty("author").GetString());

        Assert.Contains("contentHtml", (await ErrorsOf(await http.PostAsJsonAsync($"/v1/candidates/{id}/notes",
            new { contentHtml = "<p> </p><img src=x>", contentJson = (string?)null }))).Keys);

        var noteId = note.GetProperty("id").GetGuid();
        var pricila = await WithDb(db => db.AddUser(UserRole.Recruiter, "Pricila de León"));
        await _admin.PostAsJsonAsync($"/v1/openings/{_opening}/recruiters", new { userIds = new[] { pricila.Uid }, role = "support" });
        Assert.Equal(HttpStatusCode.NotFound, (await Client(pricila.AuthUid).PutAsJsonAsync($"/v1/candidates/{id}/notes/{noteId}",
            new { contentHtml = "<p>otra</p>", contentJson = (string?)null })).StatusCode);

        var edited = await BodyOf(await _admin.PutAsJsonAsync($"/v1/candidates/{id}/notes/{noteId}",
            new { contentHtml = "<p><strong>Apto</strong></p>", contentJson = (string?)null }));
        Assert.NotEqual(JsonValueKind.Null, edited.GetProperty("editedAt").ValueKind);

        var history = (await BodyOf(await _admin.GetAsync($"/v1/candidates/{id}/history"))).EnumerateArray();
        Assert.Contains(history, e => e.GetProperty("text").GetString() == "Nota agregada");
    }

    [Fact]
    public async Task Interviews_record_the_entrevistador_and_do_not_move_the_stage()
    {
        await Arrange();
        var id = (await Capture(_admin)).GetProperty("id").GetGuid();

        var interview = await BodyOf(await _admin.PostAsJsonAsync($"/v1/candidates/{id}/interviews", new
        {
            scheduledAt = "2026-10-22T16:00:00Z",
            interviewerId = _monica.Uid,
            location = "Oficinas Mitras",
            outcome = "pending",
            notes = (string?)null,
        }));
        Assert.Equal("Mónica Soto", interview.GetProperty("interviewerName").GetString());
        Assert.Equal("new", (await BodyOf(await _admin.GetAsync($"/v1/candidates/{id}"))).GetProperty("stage").GetString());

        var client = await WithDb(db => db.AddUser(UserRole.Administrator, "Otro", active: false));
        Assert.Contains("interviewerId", (await ErrorsOf(await _admin.PostAsJsonAsync($"/v1/candidates/{id}/interviews",
            new { scheduledAt = "2026-10-22T16:00:00Z", interviewerId = client.Uid, location = (string?)null, outcome = "pending", notes = (string?)null }))).Keys);
    }

    // E2E run (D-031): an offset other than Z reached Npgsql, which only writes UTC, and answered 500.
    [Fact]
    public async Task A_date_time_with_an_offset_is_stored_as_the_same_instant()
    {
        await Arrange();
        var id = (await Capture(_admin)).GetProperty("id").GetGuid();

        var interview = await BodyOf(await _admin.PostAsJsonAsync($"/v1/candidates/{id}/interviews", new
        {
            scheduledAt = "2026-10-19T10:00:00-06:00",
            interviewerId = _monica.Uid,
            location = (string?)null,
            outcome = "pending",
            notes = (string?)null,
        }));
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 16, 0, 0, TimeSpan.Zero), interview.GetProperty("scheduledAt").GetDateTimeOffset());
    }
}
