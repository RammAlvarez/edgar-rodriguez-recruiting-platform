using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Recruiting;

/// <summary>
/// The Candidates app (SPEC §7) and the candidate row of the permission matrix (DATA_MODEL §8, PLAN D77): a candidate is the
/// verified email of exactly one person with an application, reaches only their own person and documents, never validates.
/// </summary>
public sealed class CandidatePortalTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _admin = null!;
    private User _monica = null!;
    private Guid _opening;

    private async Task Arrange()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        _monica = await WithDb(async db =>
        {
            var u = await db.AddUser(UserRole.Recruiter, "Mónica Soto");
            u.Phone = "8110001122";
            await db.SaveChangesAsync();
            return u;
        });
        _admin = await AsOwner();
        _opening = (await BodyOf(await _admin.PostAsJsonAsync("/v1/openings", OpeningTests.NewOpening(heb.Uid, _monica.Uid))))
            .GetProperty("id").GetGuid();
    }

    private static string NewEmail() => $"cand-{Guid.NewGuid():N}@example.test";

    private static object Person(string email, string first = "Rosa", string? curp = null) => new
    {
        firstNames = first,
        lastNamePaternal = "Leal",
        lastNameMaternal = "Garza",
        curp,
        isForeign = false,
        email,
        source = "whatsapp",
        notes = "Nota interna del reclutador",
    };

    /// <summary>Captures a candidate with that email; returns (candidate id, person id).</summary>
    private async Task<(Guid Candidate, Guid Person)> Capture(string email, string first = "Rosa", string? curp = null, Guid? opening = null)
    {
        var c = await BodyOf(await _admin.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(opening ?? _opening, Person(email, first, curp))));
        return (c.GetProperty("id").GetGuid(), c.GetProperty("person").GetProperty("id").GetGuid());
    }

    private async Task<Dictionary<string, Guid>> DocumentTypes() =>
        (await BodyOf(await _admin.GetAsync("/v1/catalogs"))).GetProperty("documentTypes").EnumerateArray()
            .ToDictionary(t => t.GetProperty("code").GetString()!, t => t.GetProperty("id").GetGuid());

    private async Task<JsonElement> Upload(HttpClient http, Guid personId, Guid typeId, string name = "INE.jpg")
    {
        var presign = await http.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId, documentTypeId = typeId, fileName = name, contentType = "image/jpeg", sizeBytes = 2000 });
        Assert.Equal(HttpStatusCode.OK, presign.StatusCode);
        var key = (await BodyOf(presign)).GetProperty("storageKey").GetString()!;
        Factory.Storage.Put(key, 2000, "image/jpeg");
        var confirm = await http.PostAsJsonAsync($"/v1/people/{personId}/documents",
            new { documentTypeId = typeId, storageKey = key, originalFilename = name, issuedAt = (string?)null, expiresAt = "2030-01-01" });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        return await BodyOf(confirm);
    }

    [Fact]
    public async Task A_verified_email_of_a_person_with_an_application_signs_in_as_that_candidate()
    {
        await Arrange();
        var email = NewEmail();
        var (_, personId) = await Capture(email);

        var session = await AsEmail(email.ToUpperInvariant()).PostAsync("/v1/me/sessions", null);

        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var me = await BodyOf(session);
        Assert.Equal("candidate", me.GetProperty("role").GetString());
        Assert.Equal(personId, me.GetProperty("personId").GetGuid());
        Assert.Equal("Rosa Leal", me.GetProperty("displayName").GetString());
        Assert.Empty(me.GetProperty("grants").EnumerateArray());
        var login = await WithDb(db => db.Set<AuditEntry>().SingleAsync(a => a.Action == AuditAction.Login && a.RowId == personId));
        Assert.Equal("people", login.TableName);
        Assert.Null(login.UserId);
    }

    [Fact]
    public async Task No_verified_email_no_person_no_application_or_a_shared_email_is_no_access()
    {
        await Arrange();
        var email = NewEmail();
        await Capture(email);
        var lonely = NewEmail();
        await (await AsOwner()).PostAsJsonAsync("/v1/people", new { firstNames = "Sin", lastNamePaternal = "Postulación", isForeign = false, email = lonely });
        var shared = NewEmail();
        await Capture(shared, "Ana", "LEGR900101MNLLRS09");
        await (await AsOwner()).PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(_opening, new
        {
            firstNames = "Otra",
            lastNamePaternal = "Persona",
            isForeign = false,
            email = shared,
        }));

        Assert.Equal(AccessMessages.NoAccount, await DetailOf(await AsEmail(email, verified: false).GetAsync("/v1/me")));
        Assert.Equal(AccessMessages.NoAccount, await DetailOf(await AsEmail(NewEmail()).GetAsync("/v1/me")));
        Assert.Equal(AccessMessages.NoAccount, await DetailOf(await AsEmail(lonely).GetAsync("/v1/me")));
        Assert.Equal(AccessMessages.CandidateEmailShared, await DetailOf(await AsEmail(shared).GetAsync("/v1/me")));
    }

    [Fact]
    public async Task A_users_row_wins_over_a_person_with_the_same_email()
    {
        await Arrange();
        var email = NewEmail();
        await Capture(email);
        var staff = await WithDb(async db =>
        {
            var u = await db.AddUser(UserRole.Recruiter, "También Reclutadora");
            u.Email = email;
            await db.SaveChangesAsync();
            return u;
        });
        Factory.Auth.Emails[staff.AuthUid] = (email, true);

        Assert.Equal("recruiter", (await BodyOf(await Client(staff.AuthUid).GetAsync("/v1/me"))).GetProperty("role").GetString());
    }

    [Fact]
    public async Task A_candidate_cannot_call_staff_or_client_routes()
    {
        await Arrange();
        var email = NewEmail();
        var (candidateId, personId) = await Capture(email);
        var me = AsEmail(email);

        foreach (var path in new[] { "/v1/candidates", $"/v1/candidates/{candidateId}", "/v1/openings", $"/v1/people/{personId}",
                     "/v1/people?search=leal", "/v1/client-portal/openings", "/v1/users", "/v1/clients" })
            Assert.Equal(HttpStatusCode.Forbidden, (await me.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await me.PostAsJsonAsync($"/v1/candidates/{candidateId}/stage", new { stage = "suitable" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await me.PutAsJsonAsync($"/v1/people/{personId}", Person(email))).StatusCode);
    }

    [Fact]
    public async Task A_candidate_uploads_and_views_only_their_own_documents_and_never_reviews_them()
    {
        await Arrange();
        var email = NewEmail();
        var (_, personId) = await Capture(email);
        var (_, otherId) = await Capture(NewEmail(), "Otro");
        var types = await DocumentTypes();
        var me = AsEmail(email);

        var doc = await Upload(me, personId, types["INE"]);
        Assert.Equal("delivered", doc.GetProperty("status").GetString());
        var docId = doc.GetProperty("id").GetGuid();

        var mine = await BodyOf(await me.GetAsync("/v1/candidate-portal/documents"));
        Assert.Equal(personId, mine.GetProperty("personId").GetGuid());
        Assert.Equal("delivered", mine.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == "INE").GetProperty("current").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync($"/v1/people/{personId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync($"/v1/documents/{docId}/url")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await me.GetAsync($"/v1/people/{otherId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await me.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId = otherId, documentTypeId = types["INE"], fileName = "x.jpg", contentType = "image/jpeg", sizeBytes = 10 })).StatusCode);
        var others = await Upload(_admin, otherId, types["CURP"], "CURP.jpg");
        Assert.Equal(HttpStatusCode.NotFound, (await me.GetAsync($"/v1/documents/{others.GetProperty("id").GetGuid()}/url")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await me.PostAsync($"/v1/documents/{docId}/validate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await me.PostAsJsonAsync($"/v1/documents/{docId}/reject", new { reason = "x" })).StatusCode);

        // Her own view is not a third-party access; the recruiter's is logged.
        Assert.False(await WithDb(db => db.Set<DataAccessEntry>().AnyAsync(e => e.DocumentId == docId)));
        await _admin.GetAsync($"/v1/documents/{docId}/url");
        Assert.True(await WithDb(db => db.Set<DataAccessEntry>().AnyAsync(e => e.DocumentId == docId)));
    }

    [Fact]
    public async Task A_rejected_document_shows_its_reason_to_the_candidate()
    {
        await Arrange();
        var email = NewEmail();
        var (_, personId) = await Capture(email);
        var me = AsEmail(email);
        var doc = await Upload(me, personId, (await DocumentTypes())["INE"]);
        await _admin.PostAsJsonAsync($"/v1/documents/{doc.GetProperty("id").GetGuid()}/reject", new { reason = "Imagen ilegible" });

        var ine = (await BodyOf(await me.GetAsync("/v1/candidate-portal/documents"))).GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == "INE").GetProperty("current");
        Assert.Equal("rejected", ine.GetProperty("status").GetString());
        Assert.Equal("Imagen ilegible", ine.GetProperty("rejectionReason").GetString());
    }

    [Fact]
    public async Task The_profile_edits_personal_data_but_not_the_email_or_the_recruiters_fields()
    {
        await Arrange();
        var email = NewEmail();
        var (_, personId) = await Capture(email);
        var me = AsEmail(email);

        var profile = await BodyOf(await me.GetAsync("/v1/candidate-portal/profile"));
        Assert.Equal(email, profile.GetProperty("email").GetString());
        Assert.False(profile.TryGetProperty("notes", out _));
        Assert.False(profile.TryGetProperty("source", out _));

        var saved = await me.PutAsJsonAsync("/v1/candidate-portal/profile", new
        {
            firstNames = "rosa maría",
            lastNamePaternal = "leal",
            lastNameMaternal = "garza",
            curp = "LEGR900101MNLLRS09",
            nss = "12345678901",
            mobilePhone = "8112223344",
            street = "Av. Juárez",
            extNumber = "100",
            neighborhood = "Centro",
            postalCode = "64000",
            municipality = "Monterrey",
            state = "Nuevo León",
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("ROSA MARÍA", (await BodyOf(saved)).GetProperty("firstNames").GetString());
        var person = await WithDb(db => db.Set<Person>().SingleAsync(p => p.Uid == personId));
        Assert.Equal(email, person.Email);
        Assert.Equal("Nota interna del reclutador", person.Notes);
        Assert.Equal(CandidateSource.Whatsapp, person.Source);
        Assert.Equal(new DateOnly(1990, 1, 1), person.BirthDate);
        Assert.Equal(Gender.Female, person.Gender);

        var bad = await me.PutAsJsonAsync("/v1/candidate-portal/profile", new { firstNames = "Rosa", lastNamePaternal = "Leal", curp = "XX", postalCode = "1" });
        var errors = await ErrorsOf(bad);
        Assert.Contains("curp", errors.Keys);
        Assert.Contains("postalCode", errors.Keys);
    }

    [Fact]
    public async Task A_duplicate_curp_is_refused_without_naming_the_other_person()
    {
        await Arrange();
        await Capture(NewEmail(), "Ana", "LEGR900101MNLLRS09");
        var email = NewEmail();
        await Capture(email);

        var response = await AsEmail(email).PutAsJsonAsync("/v1/candidate-portal/profile",
            new { firstNames = "Rosa", lastNamePaternal = "Leal", curp = "LEGR900101MNLLRS09" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var message = await DetailOf(response);
        Assert.Equal(Platform.Recruiting.Portal.CandidatePortalEndpoints.DuplicateId, message);
        Assert.DoesNotContain("ANA", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Accepting_the_privacy_notice_is_stamped_once()
    {
        await Arrange();
        var email = NewEmail();
        await Capture(email);
        var me = AsEmail(email);
        Assert.Equal(JsonValueKind.Null, (await BodyOf(await me.GetAsync("/v1/candidate-portal/profile"))).GetProperty("privacyNoticeAcceptedAt").ValueKind);

        var first = (await BodyOf(await me.PostAsync("/v1/candidate-portal/privacy-notice", null))).GetProperty("privacyNoticeAcceptedAt").GetDateTimeOffset();
        var again = (await BodyOf(await me.PostAsync("/v1/candidate-portal/privacy-notice", null))).GetProperty("privacyNoticeAcceptedAt").GetDateTimeOffset();

        Assert.Equal(first, again);
    }

    [Fact]
    public async Task Applications_are_told_in_friendly_steps_without_internal_reasons()
    {
        await Arrange();
        var email = NewEmail();
        var (candidateId, personId) = await Capture(email);
        var me = AsEmail(email);

        var app = Assert.Single((await BodyOf(await me.GetAsync("/v1/candidate-portal/applications"))).EnumerateArray());
        Assert.Equal("HEB – AUXILIAR DE LIMPIEZA", app.GetProperty("openingName").GetString());
        Assert.Equal("received", app.GetProperty("step").GetString());
        Assert.Equal("in_process", app.GetProperty("outcome").GetString());
        Assert.Equal("Mónica Soto", app.GetProperty("recruiterName").GetString());
        Assert.Equal("8110001122", app.GetProperty("recruiterPhone").GetString());

        await _admin.PostAsJsonAsync($"/v1/candidates/{candidateId}/stage", new { stage = "interview" });
        var at = DateTimeOffset.UtcNow.AddDays(2);
        await _admin.PostAsJsonAsync($"/v1/candidates/{candidateId}/interviews", new
        {
            scheduledAt = at,
            interviewerId = _monica.Uid,
            location = "Sucursal Centro",
            outcome = "pending",
            notes = "Nota interna",
        });
        app = Assert.Single((await BodyOf(await me.GetAsync("/v1/candidate-portal/applications"))).EnumerateArray());
        Assert.Equal("interview", app.GetProperty("step").GetString());
        Assert.Equal("Sucursal Centro", app.GetProperty("nextInterviewLocation").GetString());
        Assert.False(app.TryGetProperty("notes", out _));

        await _admin.PostAsJsonAsync($"/v1/candidates/{candidateId}/stage", new { stage = "discarded", reason = "No se presentó" });
        var raw = await (await me.GetAsync("/v1/candidate-portal/applications")).Content.ReadAsStringAsync();
        app = Assert.Single(JsonDocument.Parse(raw).RootElement.EnumerateArray());
        Assert.Equal("result", app.GetProperty("step").GetString());
        Assert.Equal("closed", app.GetProperty("outcome").GetString());
        Assert.DoesNotContain("No se presentó", raw, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, app.GetProperty("nextInterviewAt").ValueKind);
        Assert.NotEqual(Guid.Empty, personId);
    }

    [Fact]
    public async Task Capturing_with_an_email_sends_the_invitation_to_the_candidates_app()
    {
        await Arrange();
        var email = NewEmail();
        var (candidateId, _) = await Capture(email);

        var sent = Assert.Single(Factory.Email.To(email));
        Assert.Equal("Tu postulación: HEB – AUXILIAR DE LIMPIEZA", sent.Subject);
        Assert.Contains("http://localhost:4301", sent.Text, StringComparison.Ordinal);
        Assert.Contains("Te registramos en el proceso de la vacante HEB – AUXILIAR DE LIMPIEZA. Te acompaña Mónica Soto.", sent.Text, StringComparison.Ordinal);
        Assert.Contains("Hola Rosa:", sent.Text, StringComparison.Ordinal);

        var resent = await BodyOf(await _admin.PostAsync($"/v1/candidates/{candidateId}/invitation", null));
        Assert.True(resent.GetProperty("sent").GetBoolean());
        Assert.Equal(2, Factory.Email.To(email).Count);
    }

    [Fact]
    public async Task A_failed_invitation_keeps_the_candidate_and_without_email_there_is_nothing_to_send()
    {
        await Arrange();
        var email = NewEmail();
        Factory.Email.Fail = true;
        try
        {
            var response = await _admin.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(_opening, Person(email)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var id = (await BodyOf(response)).GetProperty("id").GetGuid();
            Assert.False((await BodyOf(await _admin.PostAsync($"/v1/candidates/{id}/invitation", null))).GetProperty("sent").GetBoolean());
        }
        finally
        {
            Factory.Email.Fail = false;
        }

        var noEmail = await BodyOf(await _admin.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(_opening,
            new { firstNames = "Sin", lastNamePaternal = "Correo", isForeign = false })));
        var resend = await _admin.PostAsync($"/v1/candidates/{noEmail.GetProperty("id").GetGuid()}/invitation", null);
        Assert.Equal(HttpStatusCode.Conflict, resend.StatusCode);
        Assert.Equal(Platform.Recruiting.Candidates.CandidateInvitation.NoEmail, await DetailOf(resend));
    }

    [Fact]
    public async Task A_candidate_adds_the_account_to_be_paid_to_and_only_its_last_four_digits_come_back()
    {
        await Arrange();
        var email = NewEmail();
        var (_, personId) = await Capture(email);
        var me = AsEmail(email);
        var azteca = (await BodyOf(await me.GetAsync("/v1/candidate-portal/banks"))).EnumerateArray()
            .Single(b => b.GetProperty("code").GetString() == "127").GetProperty("id").GetGuid();

        var wrong = await me.PostAsJsonAsync("/v1/candidate-portal/bank-accounts",
            new { bankId = azteca, accountType = "debit_card", accountNumber = "4152 3138 0000 1238", holderName = (string?)null, alias = (string?)null });
        Assert.Contains("dígito verificador", (await ErrorsOf(wrong))["accountNumber"][0]);

        var added = await me.PostAsJsonAsync("/v1/candidate-portal/bank-accounts",
            new { bankId = azteca, accountType = "debit_card", accountNumber = "4152 3138 0000 1237", holderName = (string?)null, alias = "Mi tarjeta" });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var raw = await added.Content.ReadAsStringAsync();
        Assert.DoesNotContain("415231380000", raw);
        var account = (await BodyOf(added)).EnumerateArray().Single();
        Assert.Equal("1237", account.GetProperty("last4").GetString());
        Assert.True(account.GetProperty("isPrimary").GetBoolean());
        Assert.False(account.GetProperty("isThirdParty").GetBoolean()); // the titular defaults to the person

        var stored = await WithDb(db => db.Set<PersonBankAccount>().SingleAsync(a => a.PersonId == personId));
        Assert.DoesNotContain("4152313800001237", System.Text.Encoding.Latin1.GetString(stored.AccountNumberEnc));
        Assert.Equal("4152313800001237", Factory.Services.GetRequiredService<Platform.Core.Security.IFieldCipher>().Decrypt(stored.AccountNumberEnc));
    }
}
