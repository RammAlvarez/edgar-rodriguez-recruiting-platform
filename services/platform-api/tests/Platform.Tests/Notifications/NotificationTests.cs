using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Internal;
using Platform.Core.Notifications;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Staff;
using Platform.Recruiting.Openings;
using Platform.Tests.Payroll;
using Platform.Tests.Recruiting;
using Platform.Tests.Support;

namespace Platform.Tests.Notifications;

/// <summary>
/// The bell and the digest (S12, SPEC §3.5, PLAN D88): each trigger once, nothing repeated when a job runs again, a user sees
/// only their own items, and the daily email sends each item once.
/// </summary>
public sealed class NotificationTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _owner = null!;
    private User _monica = null!;
    private Guid _opening;

    private async Task ArrangeRecruiting()
    {
        var heb = await WithDb(db => db.AddClient("HEB"));
        _monica = await WithDb(db => db.AddUser(UserRole.Recruiter, "Mónica Soto"));
        _owner = await AsOwner();
        _opening = (await BodyOf(await _owner.PostAsJsonAsync("/v1/openings", OpeningTests.NewOpening(heb.Uid, _monica.Uid)))).GetProperty("id").GetGuid();
    }

    private async Task<(Guid Candidate, Guid Person, string Email)> Capture()
    {
        var email = $"cand-{Guid.NewGuid():N}@example.test";
        var c = await BodyOf(await _owner.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(_opening, new
        {
            firstNames = "Rosa",
            lastNamePaternal = "Leal",
            lastNameMaternal = "Garza",
            curp = (string?)null,
            isForeign = false,
            email,
            source = "whatsapp",
            notes = (string?)null,
        })));
        return (c.GetProperty("id").GetGuid(), c.GetProperty("person").GetProperty("id").GetGuid(), email);
    }

    private async Task<Dictionary<string, Guid>> DocumentTypes() =>
        (await BodyOf(await _owner.GetAsync("/v1/catalogs"))).GetProperty("documentTypes").EnumerateArray()
            .ToDictionary(t => t.GetProperty("code").GetString()!, t => t.GetProperty("id").GetGuid());

    private async Task Upload(HttpClient http, Guid personId, Guid typeId, string? expiresAt = "2030-01-01")
    {
        var presign = await http.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId, documentTypeId = typeId, fileName = "INE.jpg", contentType = "image/jpeg", sizeBytes = 2000 });
        Assert.Equal(HttpStatusCode.OK, presign.StatusCode);
        var key = (await BodyOf(presign)).GetProperty("storageKey").GetString()!;
        Factory.Storage.Put(key, 2000, "image/jpeg");
        var confirm = await http.PostAsJsonAsync($"/v1/people/{personId}/documents",
            new { documentTypeId = typeId, storageKey = key, originalFilename = "INE.jpg", issuedAt = (string?)null, expiresAt });
        Assert.True(confirm.IsSuccessStatusCode, await confirm.Content.ReadAsStringAsync());
    }

    private async Task Job(string name)
    {
        var http = Client();
        http.DefaultRequestHeaders.Add(InternalEndpoints.SecretHeader, ApiFactory.InternalSecret);
        var response = await http.PostAsync($"/internal/jobs/{name}", null);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private Task<List<Notification>> Of(Guid userId) =>
        WithDb(db => db.Set<Notification>().Where(n => n.UserId == userId).OrderBy(n => n.Id).ToListAsync());

    [Fact]
    public async Task A_candidates_upload_tells_their_recruiter_and_the_bell_reads_and_clears()
    {
        await ArrangeRecruiting();
        var (candidate, person, email) = await Capture();
        var types = await DocumentTypes();

        // The recruiter's own upload tells nobody; the candidate's does.
        await Upload(_owner, person, types["CURP"]);
        Assert.Empty(await Of(_monica.Uid));
        var me = AsEmail(email);
        Assert.Equal(HttpStatusCode.OK, (await me.PostAsync("/v1/me/sessions", null)).StatusCode);
        await me.PostAsync("/v1/candidate-portal/privacy-notice", null);
        await Upload(me, person, types["INE"]);
        var told = Assert.Single(await Of(_monica.Uid));
        Assert.Equal((NotificationKind.DocumentUploaded, $"/mis-candidatos/{candidate}"), (told.Kind, told.Link));
        Assert.StartsWith("LEAL GARZA ROSA subió", told.Title, StringComparison.Ordinal);

        var monica = Client(_monica.AuthUid);
        var bell = await BodyOf(await monica.GetAsync("/v1/notifications"));
        Assert.Equal(1, bell.GetProperty("unread").GetInt32());
        var id = bell.GetProperty("items")[0].GetProperty("id").GetGuid();
        // Someone else's item does not exist for them.
        Assert.Equal(HttpStatusCode.NotFound, (await _owner.PostAsync($"/v1/notifications/{id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await monica.PostAsync($"/v1/notifications/{id}/read", null)).StatusCode);
        Assert.Equal(0, (await BodyOf(await monica.GetAsync("/v1/notifications"))).GetProperty("unread").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await monica.PostAsync("/v1/notifications/read-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await me.GetAsync("/v1/notifications")).StatusCode);
    }

    [Fact]
    public async Task Document_expiry_tells_at_30_and_at_7_days_and_an_idle_vacante_tells_its_lider_once()
    {
        await ArrangeRecruiting();
        var (_, person, _) = await Capture();
        var types = await DocumentTypes();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
        await Upload(_owner, person, types["INE"], today.AddDays(20).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        // The vacante's last move was 10 days ago.
        await WithDb(async db =>
        {
            await db.Set<JobOpening>().Where(o => o.Uid == _opening).ExecuteUpdateAsync(s => s.SetProperty(o => o.UpdatedAt, DateTimeOffset.UtcNow.AddDays(-10)));
            await db.Database.ExecuteSqlAsync($"UPDATE platform.candidate_stage_history SET changed_at = now() - interval '10 days'");
        });

        await Job("document-expiry");
        await Job("document-expiry");
        var items = await Of(_monica.Uid);
        Assert.Equal([NotificationKind.DocumentExpiring, NotificationKind.OpeningIdle], items.Select(n => n.Kind).Order());
        Assert.Contains("10 días sin actividad", items.Single(n => n.Kind == NotificationKind.OpeningIdle).Title, StringComparison.Ordinal);

        await WithDb(db => db.Set<PersonDocument>().Where(d => d.PersonId == person)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ExpiresAt, today.AddDays(5))));
        await Job("document-expiry");
        Assert.Equal(2, (await Of(_monica.Uid)).Count(n => n.Kind == NotificationKind.DocumentExpiring));

        // The digest emails her unread items once.
        await Job("notifications-digest");
        await Job("notifications-digest");
        var mail = Assert.Single(Factory.Email.To(_monica.Email));
        Assert.Equal("Tienes 3 avisos en la plataforma", mail.Subject);
        Assert.Contains("/mis-vacantes/", mail.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_period_in_review_tells_prenomina_holders_of_that_client_and_an_invitation_welcomes_the_user()
    {
        var owner = await WithDb(db => db.AddUser(UserRole.Administrator, "Dueño", grants: PermissionModules.All.Select(m => new UserPermission { Module = m })));
        _owner = Client(owner.AuthUid);
        var ecovia = await WithDb(db => db.AddClient("ECOVIA"));
        var other = await WithDb(db => db.AddClient("HEB"));
        var (hebOnly, _) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollPrepayroll, ClientId = other.Uid });
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(ecovia.Uid)))).GetProperty("id").GetGuid();
        await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions", PayrollGroupTests.NewPosition(validFrom: "2020-01-01"));
        var period = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/periods", new { date = "2026-09-14" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await _owner.PostAsync($"/v1/payroll/periods/{period}/close-capture", null)).StatusCode);
        var told = Assert.Single(await Of(owner.Uid));
        Assert.Equal((NotificationKind.PeriodInReview, $"/prenomina?period={period}"), (told.Kind, told.Link));
        Assert.Empty(await Of(hebOnly.Uid));

        var invited = await BodyOf(await _owner.PostAsJsonAsync("/v1/users", new
        {
            displayName = "Nueva Reclutadora",
            email = $"nueva-{Guid.NewGuid():N}@example.test",
            role = "recruiter",
            clientId = (Guid?)null,
            clientContactId = (Guid?)null,
            mfaRequired = false,
            grants = (object?)null,
        }));
        var newUser = invited.GetProperty("user").GetProperty("id").GetGuid();
        Assert.Equal(NotificationKind.UserInvited, Assert.Single(await Of(newUser)).Kind);
    }

    [Fact]
    public async Task A_day_that_closes_without_the_expected_capture_tells_who_captures_for_that_client_once()
    {
        _owner = await AsOwner();
        var ecovia = await WithDb(db => db.AddClient("ECOVIA"));
        var group = (await BodyOf(await _owner.PostAsJsonAsync("/v1/payroll/groups", PayrollGroupTests.NewGroup(ecovia.Uid)))).GetProperty("id").GetGuid();
        var position = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/payroll/groups/{group}/positions", PayrollGroupTests.NewPosition(validFrom: "2020-01-01"))))
            .GetProperty("positions")[0].GetProperty("id").GetGuid();
        var site = (await BodyOf(await _owner.PostAsJsonAsync($"/v1/clients/{ecovia.Uid}/sites", new { name = "Estación Centro", zoneId = (Guid?)null })))
            .GetProperty("sites")[0].GetProperty("id").GetGuid();
        var employee = await WithDb(async db =>
        {
            var p = await db.AddPerson("ANA", "PÉREZ", "LÓPEZ");
            var e = new Employee { PersonId = p.Uid, PayrollGroupId = group, PositionId = position, Shift = "C/DÍA", HiredAt = new DateOnly(2026, 1, 1) };
            db.Add(e);
            await db.SaveChangesAsync();
            return e.Uid;
        });
        var schedule = await _owner.PostAsJsonAsync($"/v1/employees/{employee}/schedules", new
        {
            clientSiteId = site,
            weekdays = new[] { 1, 2, 3, 4, 5, 6, 7 },
            oneOffDate = (string?)null,
            startTime = "07:00:00",
            endTime = "19:00:00",
            validFrom = "2026-10-01",
            validTo = (string?)null,
            jobDescription = (string?)null,
        });
        Assert.True(schedule.IsSuccessStatusCode, await schedule.Content.ReadAsStringAsync());
        var (supervisor, _) = await SignedIn(UserRole.Administrator, new UserPermission { Module = PermissionModule.PayrollAttendance, ClientId = ecovia.Uid });

        Factory.Clock.SetMonterrey(new DateTime(2026, 10, 14, 10, 0, 0));
        await Job("attendance-lock");
        await Job("attendance-lock");
        var told = Assert.Single(await Of(supervisor.Uid));
        Assert.Equal((NotificationKind.AttendanceMissing, "ECOVIA · VALIDADORES 12 HRS: 1 sin captura el 13/10/2026"), (told.Kind, told.Title));
    }
}
