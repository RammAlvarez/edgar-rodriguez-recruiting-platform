using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Recruiting;

/// <summary>
/// The "Done when" of S4 (PLAN §2): demo path steps 1–3c of ARCHITECTURE §9 against the API; and S5's gate 1 (ARCHITECTURE
/// §4.1 steps 1–3): recruiter captures → candidate uploads INE → recruiter validates, moves to Apto → client sees the name.
/// </summary>
public sealed class DemoPathTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Steps_1_to_3c_of_the_demo_path()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await WithDb(db => db.AddUser(UserRole.Recruiter, "Mónica Soto"));
        var pricila = await WithDb(db => db.AddUser(UserRole.Recruiter, "Pricila de León"));
        var edgar = await WithDb(db => db.AddUser(UserRole.Administrator, "Edgar",
            grants: PermissionModules.All.Select(m => new UserPermission { Module = m })));
        var admin = Client(edgar.AuthUid);

        // 1. Vacantes → «Nueva vacante» for HEB, 3 auxiliares → in the table and in the client's detail.
        var vacante = await BodyOf(await admin.PostAsJsonAsync("/v1/openings", OpeningTests.NewOpening(heb.Uid, pricila.Uid, headcount: 3)));
        var vacanteId = vacante.GetProperty("id").GetGuid();
        Assert.Contains((await BodyOf(await admin.GetAsync($"/v1/openings?clientId={heb.Uid}"))).EnumerateArray(),
            o => o.GetProperty("id").GetGuid() == vacanteId);

        // 2. «Nuevo candidato» on «HEB – Auxiliar de limpieza» → validate two documents → completeness changes → a note.
        var option = Assert.Single((await BodyOf(await admin.GetAsync("/v1/openings/options"))).EnumerateArray());
        Assert.Equal("HEB – Auxiliar de limpieza", option.GetProperty("displayName").GetString());
        var candidate = await BodyOf(await admin.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(vacanteId)));
        var candidateId = candidate.GetProperty("id").GetGuid();
        var personId = candidate.GetProperty("person").GetProperty("id").GetGuid();

        var types = (await BodyOf(await admin.GetAsync("/v1/catalogs"))).GetProperty("documentTypes").EnumerateArray()
            .ToDictionary(t => t.GetProperty("code").GetString()!, t => t.GetProperty("id").GetGuid());
        var before = (await BodyOf(await admin.GetAsync($"/v1/people/{personId}/documents"))).GetProperty("completeness").GetString();
        foreach (var code in new[] { "CURP", "ACTA_NAC" })
        {
            var presign = await BodyOf(await admin.PostAsJsonAsync("/v1/files/presign-upload",
                new { personId, documentTypeId = types[code], fileName = $"{code}.pdf", contentType = "application/pdf", sizeBytes = 1000 }));
            var key = presign.GetProperty("storageKey").GetString()!;
            Factory.Storage.Put(key, 1000, "application/pdf");
            var doc = await BodyOf(await admin.PostAsJsonAsync($"/v1/people/{personId}/documents",
                new { documentTypeId = types[code], storageKey = key, originalFilename = $"{code}.pdf", issuedAt = (string?)null, expiresAt = (string?)null }));
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/v1/documents/{doc.GetProperty("id").GetGuid()}/validate", null)).StatusCode);
        }
        var after = await BodyOf(await admin.GetAsync($"/v1/people/{personId}/documents"));
        Assert.NotEqual(before, after.GetProperty("completeness").GetString());
        Assert.Equal(2, after.GetProperty("items").EnumerateArray().Count(i =>
            i.GetProperty("current").ValueKind == JsonValueKind.Object
            && i.GetProperty("current").GetProperty("status").GetString() == "validated"));

        var note = await admin.PostAsJsonAsync($"/v1/candidates/{candidateId}/notes",
            new { contentHtml = "<h2>Entrevista telefónica</h2><ul><li><p>Puntual</p></li><li><p>Disponible</p></li></ul>", contentJson = "{}" });
        Assert.Equal(HttpStatusCode.OK, note.StatusCode);
        var top = (await BodyOf(await admin.GetAsync($"/v1/candidates/{candidateId}/notes"))).EnumerateArray().First();
        Assert.StartsWith("<h2>Entrevista telefónica</h2><ul>", top.GetProperty("contentHtml").GetString());

        // 3. Kanban → drag the candidate to Apto (the portal half is S5's gate 1).
        var apto = await BodyOf(await admin.PostAsJsonAsync($"/v1/candidates/{candidateId}/stage", new { stage = "suitable" }));
        Assert.Equal("suitable", apto.GetProperty("candidate").GetProperty("stage").GetString());

        // 3c (before 3b, as the vacante is not Mónica's yet). «Asignar vacante» → the HEB vacante as Líder → in her Mis vacantes.
        var assigned = await BodyOf(await admin.PostAsJsonAsync($"/v1/recruiters/{monica.Uid}/openings",
            new { openingIds = new[] { vacanteId }, role = "lead" }));
        Assert.True(Assert.Single(assigned.GetProperty("assignments").EnumerateArray()).GetProperty("isLead").GetBoolean());
        var covering = Client(edgar.AuthUid, actingFor: monica.Uid.ToString());
        Assert.Equal(vacanteId, Assert.Single((await BodyOf(await covering.GetAsync("/v1/openings"))).EnumerateArray()).GetProperty("id").GetGuid());
        var activity = (await BodyOf(await admin.GetAsync($"/v1/openings/{vacanteId}/events"))).EnumerateArray().First();
        Assert.Equal("lead_changed", activity.GetProperty("kind").GetString());
        Assert.Equal("Mónica Soto", activity.GetProperty("recruiterName").GetString());

        // 3b. «Ver como reclutador» → move one of her candidates to Entrevista → «Edgar (cubriendo a Mónica Soto)».
        var hers = await BodyOf(await covering.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(vacanteId,
            new { firstNames = "Rosa", lastNamePaternal = "Leal", isForeign = false })));
        Assert.Equal(monica.Uid, hers.GetProperty("recruiterId").GetGuid());
        await covering.PostAsJsonAsync($"/v1/candidates/{hers.GetProperty("id").GetGuid()}/stage", new { stage = "interview" });
        var entry = (await BodyOf(await covering.GetAsync($"/v1/candidates/{hers.GetProperty("id").GetGuid()}/history"))).EnumerateArray().First();
        Assert.Equal("interview", entry.GetProperty("toStage").GetString());
        Assert.Equal("Edgar (cubriendo a Mónica Soto)", entry.GetProperty("author").GetString());
    }

    [Fact]
    public async Task Gate_1_candidate_to_apto_seen_in_the_portal()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        var monica = await WithDb(db => db.AddUser(UserRole.Recruiter, "Mónica Soto"));
        var owner = await AsOwner();
        var vacanteId = (await BodyOf(await owner.PostAsJsonAsync("/v1/openings", OpeningTests.NewOpening(heb.Uid, monica.Uid))))
            .GetProperty("id").GetGuid();
        var laura = await WithDb(db => db.AddUser(UserRole.Client, "Laura (HEB)", clientId: heb.Uid));
        var portal = Client(laura.AuthUid);

        // 1. The recruiter captures the candidate with an email → the invitation to the Candidates app goes out.
        var recruiter = Client(monica.AuthUid);
        var email = $"rosa-{Guid.NewGuid():N}@example.test";
        var captured = await BodyOf(await recruiter.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(vacanteId,
            new { firstNames = "Rosa", lastNamePaternal = "Leal", lastNameMaternal = "Garza", isForeign = false, email })));
        var candidateId = captured.GetProperty("id").GetGuid();
        var personId = captured.GetProperty("person").GetProperty("id").GetGuid();
        Assert.Single(Factory.Email.To(email));
        var masked = Assert.Single((await BodyOf(await portal.GetAsync($"/v1/client-portal/openings/{vacanteId}")))
            .GetProperty("candidates").EnumerateArray());
        Assert.Equal("R. L.", masked.GetProperty("name").GetString());

        // 2. The candidate signs in with that email and uploads her INE from the Candidates app.
        var candidate = AsEmail(email);
        Assert.Equal("candidate", (await BodyOf(await candidate.PostAsync("/v1/me/sessions", null))).GetProperty("role").GetString());
        var ine = (await BodyOf(await candidate.GetAsync("/v1/candidate-portal/documents"))).GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == "INE").GetProperty("documentTypeId").GetGuid();
        var presign = await BodyOf(await candidate.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId, documentTypeId = ine, fileName = "ine-frente.jpg", contentType = "image/jpeg", sizeBytes = 350_000 }));
        var key = presign.GetProperty("storageKey").GetString()!;
        Factory.Storage.Put(key, 350_000, "image/jpeg");
        var doc = await BodyOf(await candidate.PostAsJsonAsync($"/v1/people/{personId}/documents",
            new { documentTypeId = ine, storageKey = key, originalFilename = "ine-frente.jpg", issuedAt = (string?)null, expiresAt = "2031-12-31" }));
        Assert.Equal("delivered", doc.GetProperty("status").GetString());

        // 3. The recruiter sees it delivered, validates it and moves the candidate to Apto → the client sees her name.
        var checklist = await BodyOf(await recruiter.GetAsync($"/v1/people/{personId}/documents"));
        var current = checklist.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("code").GetString() == "INE").GetProperty("current");
        Assert.Equal("delivered", current.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PostAsync($"/v1/documents/{current.GetProperty("id").GetGuid()}/validate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PostAsJsonAsync($"/v1/candidates/{candidateId}/stage", new { stage = "suitable" })).StatusCode);

        var seen = Assert.Single((await BodyOf(await portal.GetAsync($"/v1/client-portal/openings/{vacanteId}")))
            .GetProperty("candidates").EnumerateArray());
        Assert.True(seen.GetProperty("isIdentified").GetBoolean());
        Assert.Equal("LEAL GARZA ROSA", seen.GetProperty("name").GetString());
        Assert.NotEqual("0.00", seen.GetProperty("documentsCompleteness").GetString());
        var mine = Assert.Single((await BodyOf(await candidate.GetAsync("/v1/candidate-portal/applications"))).EnumerateArray());
        Assert.Equal("selected", mine.GetProperty("outcome").GetString());
        Assert.Equal("validated", (await BodyOf(await candidate.GetAsync("/v1/candidate-portal/documents"))).GetProperty("items")
            .EnumerateArray().Single(i => i.GetProperty("code").GetString() == "INE").GetProperty("current").GetProperty("status").GetString());
    }
}
