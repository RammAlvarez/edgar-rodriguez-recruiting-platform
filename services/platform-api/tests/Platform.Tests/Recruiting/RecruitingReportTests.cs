using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Audit;
using Platform.Core.Users;
using Platform.Recruiting.Reports;
using Platform.Tests.Support;

namespace Platform.Tests.Recruiting;

/// <summary>
/// Reportes de reclutamiento (SPEC §4.7, PLAN D80) over one small arranged dataset, every figure computed by hand:
/// <list type="bullet">
/// <item>O1 HEB – CAJERO, líder Mónica, 2 solicitadas, requested 30 days ago: c1 and c2 hired (so it is filled today, 30 days to
/// fill), c3 No apto «No pasó entrevista». All three are Mónica's.</item>
/// <item>O2 HEB – AUXILIAR, líder Pedro, 1 solicitada, requested 10 days ago: c4 Apto (from WhatsApp), c5 Descartado «No contestó».</item>
/// <item>O3 OXXO – AUXILIAR, líder Mónica, 3 solicitadas, requested 90 days ago (outside the 60-day range): c6 Nuevo.</item>
/// </list>
/// </summary>
public sealed class RecruitingReportTests(ApiFactory factory) : ApiTest(factory)
{
    private HttpClient _owner = null!;
    private Guid _heb, _oxxo, _monica, _pedro, _o1, _o2, _o3, _c4, _c6;
    private DateOnly _today;

    private string Range => $"from={_today.AddDays(-60):yyyy-MM-dd}&to={_today:yyyy-MM-dd}";

    private async Task Arrange()
    {
        _today = new SystemClock().Today();
        // The fake bucket is shared by the whole test run (tests run one at a time): start each test with no exports.
        foreach (var key in Factory.Storage.Contents.Keys.Where(k => k.Item1 == StorageBucket.Exports))
            Factory.Storage.Contents.TryRemove(key, out _);
        _heb = (await WithDb(db => db.AddClient("HEB"))).Uid;
        _oxxo = (await WithDb(db => db.AddClient("OXXO"))).Uid;
        _monica = (await WithDb(db => db.AddUser(UserRole.Recruiter, "Mónica Soto"))).Uid;
        _pedro = (await WithDb(db => db.AddUser(UserRole.Recruiter, "Pedro Salas"))).Uid;
        _owner = await AsOwner();

        _o1 = await Opening(_heb, _monica, "Cajero", 2, 30);
        _o2 = await Opening(_heb, _pedro, "Auxiliar", 1, 10);
        _o3 = await Opening(_oxxo, _monica, "Auxiliar", 3, 90);

        var c1 = await Capture(_o1, "Ana");
        var c2 = await Capture(_o1, "Beto");
        var c3 = await Capture(_o1, "Carla");
        _c4 = await Capture(_o2, "Diego", "whatsapp");
        var c5 = await Capture(_o2, "Elena");
        _c6 = await Capture(_o3, "Fer");
        await Move(c1, "hired");
        await Move(c2, "hired");
        await Move(c3, "not_suitable", "No pasó entrevista");
        await Move(_c4, "suitable");
        await Move(c5, "discarded", "No contestó");
    }

    private async Task<Guid> Opening(Guid client, Guid lead, string position, int headcount, int daysAgo)
    {
        var body = OpeningTests.NewOpening(client, lead, position, headcount, _today.AddDays(-daysAgo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var response = await _owner.PostAsJsonAsync("/v1/openings", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await BodyOf(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> Capture(Guid opening, string first, string source = "facebook")
    {
        var response = await _owner.PostAsJsonAsync("/v1/candidates", CandidateTests.NewCandidate(opening,
            new { firstNames = first, lastNamePaternal = "Garza", lastNameMaternal = "Soto", isForeign = false }, source: source));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await BodyOf(response)).GetProperty("id").GetGuid();
    }

    private async Task Move(Guid candidate, string stage, string? reason = null)
    {
        var response = await _owner.PostAsJsonAsync($"/v1/candidates/{candidate}/stage",
            new { stage, reason, hiredAt = stage == "hired" ? _today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<JsonElement> Get(string path) => await BodyOf(await _owner.GetAsync($"/v1/recruiting-reports/{path}"));

    /// <summary>A rate or average (decimals travel as strings).</summary>
    private static decimal? Dec(JsonElement e, string name) =>
        Str(e, name) is { } v ? decimal.Parse(v, CultureInfo.InvariantCulture) : null;

    private static string? Str(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).ToString();

    [Fact]
    public async Task Rendimiento_por_reclutador_groups_each_recruiters_vacantes_in_the_range()
    {
        await Arrange();
        var groups = (await Get($"recruiter-performance?{Range}")).GetProperty("recruiters").EnumerateArray().ToList();

        Assert.Equal(["Mónica Soto", "Pedro Salas"], groups.Select(g => g.GetProperty("recruiterName").GetString()));
        var monica = groups[0];
        Assert.Equal(1, monica.GetProperty("assigned").GetInt32()); // O3 was requested before the range
        Assert.Equal(1, monica.GetProperty("filled").GetInt32());
        Assert.Equal(1.00m, Dec(monica, "effectiveness"));
        Assert.Equal(30.0m, Dec(monica, "avgDaysToFill"));
        var o1 = monica.GetProperty("openings").EnumerateArray().Single();
        Assert.Equal("HEB – CAJERO", o1.GetProperty("openingName").GetString());
        Assert.Equal((3, 2, 2), (o1.GetProperty("presented").GetInt32(), o1.GetProperty("suitable").GetInt32(), o1.GetProperty("hired").GetInt32()));
        Assert.Equal(0.66m, Dec(o1, "conversion")); // 2 of 3, truncated like the Reclutadores cards
        Assert.Equal(30, o1.GetProperty("daysToFill").GetInt32());
        Assert.True(o1.GetProperty("isLead").GetBoolean());

        var pedro = groups[1];
        Assert.Equal(0.00m, Dec(pedro, "effectiveness"));
        Assert.Null(Str(pedro, "avgDaysToFill"));
        var o2 = pedro.GetProperty("openings").EnumerateArray().Single();
        Assert.Equal((2, 1, 0), (o2.GetProperty("presented").GetInt32(), o2.GetProperty("suitable").GetInt32(), o2.GetProperty("hired").GetInt32()));
        Assert.Null(Str(o2, "daysToFill"));

        var onlyPedro = (await Get($"recruiter-performance?{Range}&recruiterId={_pedro}")).GetProperty("recruiters").EnumerateArray();
        Assert.Equal("Pedro Salas", Assert.Single(onlyPedro).GetProperty("recruiterName").GetString());
        var wholeYear = (await Get($"recruiter-performance?from={_today.AddDays(-120):yyyy-MM-dd}&to={_today:yyyy-MM-dd}")).GetProperty("recruiters");
        Assert.Equal(2, wholeYear.EnumerateArray().First().GetProperty("assigned").GetInt32());
    }

    [Fact]
    public async Task Resumen_counts_the_range_and_the_previous_equal_range_and_splits_by_client()
    {
        await Arrange();
        var r = await Get($"summary?{Range}");

        var now = r.GetProperty("current");
        Assert.Equal((2, 0, 1, 1), (now.GetProperty("openings").GetInt32(), now.GetProperty("open").GetInt32(),
            now.GetProperty("inProgress").GetInt32(), now.GetProperty("filled").GetInt32()));
        Assert.Equal((5, 3, 2), (now.GetProperty("candidates").GetInt32(), now.GetProperty("suitable").GetInt32(), now.GetProperty("hired").GetInt32()));
        Assert.Equal(0.40m, Dec(now, "conversion"));
        Assert.Equal(30.0m, Dec(now, "avgDaysToFill"));

        // The previous 61 days end the day before the range: O3 (90 days ago) and its one candidate.
        Assert.Equal(_today.AddDays(-121).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.GetProperty("previousFrom").GetString());
        Assert.Equal(_today.AddDays(-61).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.GetProperty("previousTo").GetString());
        var before = r.GetProperty("previous");
        Assert.Equal((1, 1, 0), (before.GetProperty("openings").GetInt32(), before.GetProperty("candidates").GetInt32(), before.GetProperty("hired").GetInt32()));
        Assert.Null(Str(before, "avgDaysToFill"));

        var clients = r.GetProperty("clients").EnumerateArray().ToList();
        Assert.Equal("HEB", Assert.Single(clients).GetProperty("clientName").GetString());

        var oxxo = await Get($"summary?{Range}&clientId={_oxxo}");
        Assert.Equal(0, oxxo.GetProperty("current").GetProperty("openings").GetInt32());
        Assert.Null(Str(oxxo.GetProperty("current"), "conversion"));
    }

    [Fact]
    public async Task Vacantes_abiertas_is_a_snapshot_oldest_first_with_stage_counts_and_the_stale_flag()
    {
        await Arrange();
        // O3 has had no movement for 20 days.
        await WithDb(async db =>
        {
            await db.Database.ExecuteSqlAsync($"UPDATE platform.candidate_stage_history SET changed_at = changed_at - interval '20 days' WHERE candidate_id = {_c6}");
            await db.Database.ExecuteSqlAsync($"UPDATE platform.job_openings SET updated_at = updated_at - interval '20 days' WHERE uid = {_o3}");
            return 0;
        });

        var r = await Get("open-openings");
        var rows = r.GetProperty("openings").EnumerateArray().ToList();
        Assert.Equal([_o3, _o2], rows.Select(o => o.GetProperty("openingId").GetGuid())); // O1 is filled
        Assert.Equal(90, rows[0].GetProperty("daysOpen").GetInt32());
        Assert.Equal(1, rows[0].GetProperty("new").GetInt32());
        Assert.Equal(20, rows[0].GetProperty("idleDays").GetInt32());
        Assert.True(rows[0].GetProperty("stale").GetBoolean());
        Assert.Equal("Mónica Soto", rows[0].GetProperty("leadRecruiterName").GetString());

        Assert.Equal((0, 1, 0), (rows[1].GetProperty("new").GetInt32(), rows[1].GetProperty("suitable").GetInt32(), rows[1].GetProperty("hired").GetInt32()));
        Assert.Equal(0, rows[1].GetProperty("idleDays").GetInt32());
        Assert.False(rows[1].GetProperty("stale").GetBoolean());

        var pedro = (await Get($"open-openings?recruiterId={_pedro}")).GetProperty("openings").EnumerateArray();
        Assert.Equal(_o2, Assert.Single(pedro).GetProperty("openingId").GetGuid());
    }

    [Fact]
    public async Task Embudo_counts_entries_per_stage_what_moved_on_what_was_lost_and_why()
    {
        await Arrange();
        // Diego waited 3 days in Nuevo before becoming Apto.
        await WithDb(db => db.Database.ExecuteSqlAsync(
            $"UPDATE platform.candidate_stage_history SET changed_at = changed_at - interval '3 days' WHERE candidate_id = {_c4} AND to_stage = 'new'"));

        var r = await Get($"stage-funnel?{Range}&clientId={_heb}");
        var stages = r.GetProperty("stages").EnumerateArray().ToDictionary(s => s.GetProperty("stage").GetString()!);
        Assert.Equal(["new", "contacted", "interview", "suitable", "hired"], stages.Keys);
        var nuevo = stages["new"];
        Assert.Equal((5, 3, 2), (nuevo.GetProperty("entered").GetInt32(), nuevo.GetProperty("advanced").GetInt32(), nuevo.GetProperty("lost").GetInt32()));
        Assert.Equal(0.60m, Dec(nuevo, "advanceRate"));
        Assert.Equal(0.6m, Dec(nuevo, "avgDaysInStage")); // (3 + four moves made at once) ÷ 5
        Assert.Equal(0, stages["contacted"].GetProperty("entered").GetInt32());
        Assert.Null(Str(stages["contacted"], "advanceRate"));
        Assert.Equal(1, stages["suitable"].GetProperty("entered").GetInt32());
        Assert.Equal(2, stages["hired"].GetProperty("entered").GetInt32());
        Assert.Null(Str(stages["hired"], "advanced"));

        var losses = r.GetProperty("losses").EnumerateArray().ToList();
        Assert.Equal(["not_suitable", "discarded"], losses.Select(l => l.GetProperty("outcome").GetString()));
        Assert.Equal("No pasó entrevista", losses[0].GetProperty("reasons").EnumerateArray().Single().GetProperty("reason").GetString());
        Assert.Equal("No contestó", losses[1].GetProperty("reasons").EnumerateArray().Single().GetProperty("reason").GetString());

        var monica = (await Get($"stage-funnel?{Range}&recruiterId={_monica}")).GetProperty("stages").EnumerateArray().First();
        Assert.Equal(4, monica.GetProperty("entered").GetInt32()); // her three on O1 and Fer on O3
    }

    [Fact]
    public async Task Fuentes_lists_every_source_by_conversion()
    {
        await Arrange();
        var sources = (await Get($"sources?{Range}")).GetProperty("sources").EnumerateArray().ToList();

        var facebook = sources.Single(s => s.GetProperty("source").GetString() == "facebook");
        Assert.Equal((5, 2, 2), (facebook.GetProperty("candidates").GetInt32(), facebook.GetProperty("suitable").GetInt32(), facebook.GetProperty("hired").GetInt32()));
        Assert.Equal(0.40m, Dec(facebook, "conversion"));
        Assert.Equal(0.0m, Dec(facebook, "avgDaysToHire")); // captured and hired today
        var whatsapp = sources.Single(s => s.GetProperty("source").GetString() == "whatsapp");
        Assert.Equal(0.00m, Dec(whatsapp, "conversion"));
        Assert.Equal("facebook", sources[0].GetProperty("source").GetString());
        Assert.Equal(6, sources.Count); // the six fuentes, none «Sin fuente»
    }

    [Fact]
    public async Task Vacantes_por_cliente_has_the_fill_rate_and_a_total_row()
    {
        await Arrange();
        var r = await Get($"client-openings?{Range}");
        var heb = Assert.Single(r.GetProperty("clients").EnumerateArray());
        Assert.Equal((2, 3, 2, 0, 5), (heb.GetProperty("openings").GetInt32(), heb.GetProperty("headcountRequested").GetInt32(),
            heb.GetProperty("hired").GetInt32(), heb.GetProperty("cancelled").GetInt32(), heb.GetProperty("candidatesPresented").GetInt32()));
        Assert.Equal(0.66m, Dec(heb, "fillRate"));
        Assert.Equal(30.0m, Dec(heb, "avgDaysToFill"));
        var total = r.GetProperty("total");
        Assert.Equal("Total", total.GetProperty("clientName").GetString());
        Assert.Equal(JsonValueKind.Null, total.GetProperty("clientId").ValueKind);
        Assert.Equal(2, total.GetProperty("openings").GetInt32());
    }

    [Fact]
    public async Task The_pickers_list_every_client_and_whoever_recruits()
    {
        await Arrange();
        var options = await Get("options");
        Assert.Equal(["HEB", "OXXO"], options.GetProperty("clients").EnumerateArray().Select(c => c.GetProperty("name").GetString()));
        var names = options.GetProperty("recruiters").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Equal(["Mónica Soto", "Pedro Salas"], names); // the owner neither leads nor owns a candidate
    }

    [Fact]
    public async Task The_default_range_is_the_current_year_and_bad_filters_are_refused()
    {
        await Arrange();
        var filters = (await Get("summary")).GetProperty("filters");
        Assert.Equal($"{_today.Year}-01-01", filters.GetProperty("from").GetString());
        Assert.Equal(_today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), filters.GetProperty("to").GetString());

        Assert.Contains("to", (await ErrorsOf(await _owner.GetAsync($"/v1/recruiting-reports/summary?from={_today:yyyy-MM-dd}&to={_today.AddDays(-1):yyyy-MM-dd}"))).Keys);
        Assert.Contains("clientId", (await ErrorsOf(await _owner.GetAsync($"/v1/recruiting-reports/summary?clientId={Guid.NewGuid()}"))).Keys);
    }

    [Fact]
    public async Task Only_an_administrador_with_reportes_de_reclutamiento_sees_them()
    {
        var recruiter = await As(UserRole.Recruiter);
        var admin = await As(UserRole.Administrator, PermissionModule.Recruiting, PermissionModule.Users);
        foreach (var client in new[] { recruiter, admin })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/v1/recruiting-reports/recruiter-performance")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/v1/recruiting-reports/exports",
                new { report = "summary", format = "xlsx" })).StatusCode);
        }
        var reports = await As(UserRole.Administrator, PermissionModule.RecruitingReports);
        Assert.Equal(HttpStatusCode.OK, (await reports.GetAsync("/v1/recruiting-reports/recruiter-performance")).StatusCode);
    }

    [Fact]
    public async Task An_export_writes_the_file_to_exports_returns_a_download_and_is_audited()
    {
        await Arrange();
        var response = await _owner.PostAsJsonAsync("/v1/recruiting-reports/exports", new
        {
            report = "recruiter_performance",
            format = "xlsx",
            from = _today.AddDays(-60).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to = _today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var export = await BodyOf(response);
        var fileName = export.GetProperty("fileName").GetString()!;
        Assert.Equal($"Rendimiento por reclutador {_today.AddDays(-60):yyyy-MM-dd} a {_today:yyyy-MM-dd}.xlsx", fileName);

        var (key, bytes) = Factory.Storage.Contents.Single(c => c.Key.Item1 == StorageBucket.Exports);
        Assert.StartsWith("recruiting-reports/", key.Item2);
        Assert.Contains(Factory.Storage.Downloads, d => d.Key == key.Item2 && d.DownloadAs == fileName);
        using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
        {
            var sheet = workbook.Worksheet(1);
            Assert.Equal("Rendimiento por reclutador", sheet.Cell(1, 1).GetString());
            Assert.Contains("Todos los clientes", sheet.Cell(2, 1).GetString());
            var used = sheet.CellsUsed().ToList();
            Assert.Contains(used, c => c.GetString() == "Mónica Soto");
            var conversion = used.Single(c => c.GetString() == "HEB – CAJERO").WorksheetRow().Cell(8);
            Assert.Equal(0.66, conversion.GetDouble(), 6); // a number, formatted as a percentage
            Assert.Equal("0%", conversion.Style.NumberFormat.Format);
        }

        var audit = await WithDb(db => db.Set<AuditEntry>().SingleAsync(a => a.Action == AuditAction.Export));
        Assert.Equal("recruiter_performance", audit.TableName);
        Assert.Contains(key.Item2, audit.After);
    }

    [Theory]
    [InlineData("recruiter_performance")]
    [InlineData("summary")]
    [InlineData("open_openings")]
    [InlineData("stage_funnel")]
    [InlineData("sources")]
    [InlineData("client_openings")]
    public async Task Every_report_exports_to_pdf_and_excel(string report)
    {
        await Arrange();
        foreach (var format in new[] { "pdf", "xlsx" })
            Assert.Equal(HttpStatusCode.OK, (await _owner.PostAsJsonAsync("/v1/recruiting-reports/exports", new { report, format })).StatusCode);
        var files = Factory.Storage.Contents.Where(c => c.Key.Item1 == StorageBucket.Exports).Select(c => c.Value).ToList();
        Assert.Contains(files, f => f.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        Assert.Contains(files, f => f.AsSpan(0, 2).SequenceEqual("PK"u8));
    }
}
