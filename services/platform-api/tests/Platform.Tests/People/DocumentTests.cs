using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Audit;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.People;

/// <summary>SPEC §3.2 document vault: presign → upload → confirm, versions, review, completeness and the download log.</summary>
public sealed class DocumentTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _admin = null!;
    private Guid _person;
    private Dictionary<string, string> _types = null!;

    private async Task Arrange()
    {
        _admin = await AsOwner();
        _person = (await BodyOf(await _admin.PostAsJsonAsync("/v1/people", PeopleTests.NewPerson()))).GetProperty("id").GetGuid();
        _types = (await BodyOf(await _admin.GetAsync("/v1/catalogs"))).GetProperty("documentTypes").EnumerateArray()
            .ToDictionary(t => t.GetProperty("code").GetString()!, t => t.GetProperty("id").GetString()!);
    }

    /// <summary>The browser's three steps, with the fake bucket standing in for the PUT.</summary>
    private async Task<HttpResponseMessage> Upload(string code, long size = 200_000, string contentType = "application/pdf",
        string fileName = "ine frente.pdf", string? issuedAt = null, string? expiresAt = null)
    {
        var presign = await BodyOf(await _admin.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId = _person, documentTypeId = _types[code], fileName, contentType, sizeBytes = size }));
        var key = presign.GetProperty("storageKey").GetString()!;
        Factory.Storage.Put(key, size, contentType);
        return await _admin.PostAsJsonAsync($"/v1/people/{_person}/documents",
            new { documentTypeId = _types[code], storageKey = key, originalFilename = fileName, issuedAt, expiresAt });
    }

    private async Task<JsonElement> Checklist() => await BodyOf(await _admin.GetAsync($"/v1/people/{_person}/documents"));

    [Fact]
    public async Task The_api_decides_the_key_and_signs_the_content_type()
    {
        await Arrange();

        var presign = await BodyOf(await _admin.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId = _person, documentTypeId = _types["INE"], fileName = "../../etc/passwd.pdf", contentType = "application/pdf", sizeBytes = 1000 }));

        Assert.Matches($"^people/{_person}/INE/[0-9a-f-]{{36}}\\.pdf$", presign.GetProperty("storageKey").GetString());
        Assert.Equal("application/pdf", presign.GetProperty("headers").GetProperty("Content-Type").GetString());
    }

    [Theory]
    [InlineData("application/x-msdownload", 1000, "contentType")]
    [InlineData("application/pdf", 10 * 1024 * 1024 + 1, "sizeBytes")]
    public async Task Only_pdf_jpg_png_up_to_10_mb(string contentType, long size, string field)
    {
        await Arrange();
        var response = await _admin.PostAsJsonAsync("/v1/files/presign-upload",
            new { personId = _person, documentTypeId = _types["INE"], fileName = "x", contentType, sizeBytes = size });
        Assert.Contains(field, (await ErrorsOf(response)).Keys);
    }

    [Fact]
    public async Task Confirm_records_a_delivered_document_with_a_sanitized_name()
    {
        await Arrange();

        var doc = await BodyOf(await Upload("INE", fileName: "C:\\fotos\\INE <frente>.PDF", expiresAt: "2030-12-31"));

        Assert.Equal("delivered", doc.GetProperty("status").GetString());
        Assert.Equal("INE _frente_.pdf", doc.GetProperty("originalFilename").GetString());
        Assert.Equal(1, doc.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Confirm_refuses_keys_of_another_person_missing_objects_and_files_that_break_the_limits()
    {
        await Arrange();
        var other = (await BodyOf(await _admin.PostAsJsonAsync("/v1/people", PeopleTests.NewPerson(curp: null, rfc: null))))
            .GetProperty("id").GetGuid();
        var foreignKey = $"people/{other}/INE/{Guid.NewGuid()}.pdf";
        Factory.Storage.Put(foreignKey, 1000, "application/pdf");
        Assert.Contains("storageKey", (await ErrorsOf(await _admin.PostAsJsonAsync($"/v1/people/{_person}/documents",
            new { documentTypeId = _types["INE"], storageKey = foreignKey, originalFilename = "a.pdf" }))).Keys);

        var missing = $"people/{_person}/INE/{Guid.NewGuid()}.pdf";
        Assert.Equal(["No encontramos el archivo; vuelve a subirlo."], (await ErrorsOf(await _admin.PostAsJsonAsync(
            $"/v1/people/{_person}/documents", new { documentTypeId = _types["INE"], storageKey = missing, originalFilename = "a.pdf" })))["storageKey"]);

        // The browser sent more than it announced: the object is deleted and nothing is recorded.
        var big = $"people/{_person}/CURP/{Guid.NewGuid()}.pdf";
        Factory.Storage.Put(big, 11 * 1024 * 1024, "application/pdf");
        var tooBig = await _admin.PostAsJsonAsync($"/v1/people/{_person}/documents",
            new { documentTypeId = _types["CURP"], storageKey = big, originalFilename = "curp.pdf" });
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.DoesNotContain(Factory.Storage.Objects.Keys, k => k.Item2 == big);
    }

    [Fact]
    public async Task A_reupload_is_a_new_version_and_the_previous_one_is_kept()
    {
        await Arrange();
        var first = (await BodyOf(await Upload("CURP"))).GetProperty("id").GetGuid();

        var second = await BodyOf(await Upload("CURP", fileName: "curp nueva.pdf"));

        Assert.Equal(2, second.GetProperty("version").GetInt32());
        var current = (await Checklist()).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("code").GetString() == "CURP");
        Assert.Equal(second.GetProperty("id").GetGuid(), current.GetProperty("current").GetProperty("id").GetGuid());
        var previous = await WithDb(db => db.Set<PersonDocument>().SingleAsync(d => d.Uid == first));
        Assert.Equal(second.GetProperty("id").GetGuid(), previous.SupersededBy);

        var stale = await _admin.PostAsync($"/v1/documents/{first}/validate", null);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Completeness_counts_required_documents_delivered_or_validated()
    {
        await Arrange();
        // The default candidate template requires INE, CURP, RFC, NSS, ACTA_NAC and DOMICILIO.
        Assert.Equal("0.00", (await Checklist()).GetProperty("completeness").GetString());

        var ine = (await BodyOf(await Upload("INE", expiresAt: "2030-01-01"))).GetProperty("id").GetGuid();
        await Upload("CURP");
        var rfc = (await BodyOf(await Upload("RFC"))).GetProperty("id").GetGuid();
        await Upload("CV");
        await _admin.PostAsync($"/v1/documents/{ine}/validate", null);
        Assert.Equal("50.00", (await Checklist()).GetProperty("completeness").GetString());

        Assert.Equal(["Indica el motivo del rechazo; la persona lo verá."],
            (await ErrorsOf(await _admin.PostAsJsonAsync($"/v1/documents/{rfc}/reject", new { reason = " " })))["reason"]);
        await _admin.PostAsJsonAsync($"/v1/documents/{rfc}/reject", new { reason = "Ilegible" });

        var checklist = await Checklist();
        Assert.Equal("33.33", checklist.GetProperty("completeness").GetString());
        var cv = checklist.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("code").GetString() == "CV");
        Assert.False(cv.GetProperty("isRequired").GetBoolean());
    }

    [Fact]
    public async Task Expiry_defaults_from_the_document_type_and_expired_files_are_refused()
    {
        await Arrange();
        var issued = DateOnly.FromDateTime(DateTime.UtcNow);
        var domicilio = await BodyOf(await Upload("DOMICILIO", issuedAt: issued.ToString("O", CultureInfo.InvariantCulture)));
        Assert.Equal(issued.AddDays(90).ToString("O", CultureInfo.InvariantCulture), domicilio.GetProperty("expiresAt").GetString());

        var expired = await Upload("INE", expiresAt: "2020-01-01");
        Assert.Equal(["El documento ya está vencido; sube uno vigente."], (await ErrorsOf(expired))["expiresAt"]);
    }

    [Fact]
    public async Task Viewing_and_downloading_are_presigned_for_15_minutes_and_logged()
    {
        await Arrange();
        var doc = (await BodyOf(await Upload("CURP", fileName: "curp.pdf"))).GetProperty("id").GetGuid();

        var view = await BodyOf(await _admin.GetAsync($"/v1/documents/{doc}/url"));
        var download = await _admin.GetAsync($"/v1/documents/{doc}/url?download=true");

        Assert.InRange(view.GetProperty("expiresAt").GetDateTimeOffset() - DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Contains(Factory.Storage.Downloads, d => d.DownloadAs == "curp.pdf");
        var log = await WithDb(db => db.Set<DataAccessEntry>().Where(e => e.DocumentId == doc).OrderBy(e => e.Id).ToListAsync());
        Assert.Equal([DataAccessKind.ViewDocument, DataAccessKind.DownloadDocument], log.Select(e => e.Access));
        Assert.All(log, e => Assert.Equal(_person, e.PersonId));
    }

    [Fact]
    public async Task Clients_never_reach_documents_and_recruiters_only_those_of_their_candidates()
    {
        await Arrange();
        var doc = (await BodyOf(await Upload("CURP"))).GetProperty("id").GetGuid();

        var client = await As(UserRole.Client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/v1/documents/{doc}/url")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/v1/people/{_person}/documents")).StatusCode);

        // PLAN D73: this person is nobody's candidate, so a recruiter cannot tell it exists.
        var recruiter = await As(UserRole.Recruiter);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/v1/documents/{doc}/url")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/v1/people/{_person}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.PostAsync($"/v1/documents/{doc}/validate", null)).StatusCode);
    }
}
