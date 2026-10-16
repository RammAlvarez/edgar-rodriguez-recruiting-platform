using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Http;
using Platform.Core.Reports;
using Platform.Core.Users;

namespace Platform.Recruiting.Reports;

/// <summary>
/// Reportes de reclutamiento (SPEC §4.7): one GET per report with the filters Cliente, Reclutador and the range, and one export
/// to Excel or PDF. Administrador with <c>recruiting_reports</c> only — always all clients (D67); Reclutadores never (§4.8).
/// </summary>
public static class ReportEndpoints
{
    /// <summary>The reports' cache tag; the screen also tags them with vacantes and candidatos, which change their figures.</summary>
    public const string Tag = "recruiting-reports";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.RecruitingReports);

    public const string UnknownFilter = "El cliente o el reclutador del filtro no existe.";
    public const string RangeReversed = "La fecha final es anterior a la inicial.";

    /// <summary>A presigned download lives as long as a document view (the file stays in <c>exports</c> 90 days).</summary>
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(10);

    public static IEndpointRouteBuilder MapRecruitingReportEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/recruiting-reports").WithTags("RecruitingReports");

        Get(g, "/recruiter-performance", "GetRecruiterPerformanceReport", (db, f, _, ct) => ReportQueries.RecruiterPerformance(db, f, ct));
        Get(g, "/summary", "GetRecruitingSummaryReport", (db, f, _, ct) => ReportQueries.Summary(db, f, ct));
        Get(g, "/open-openings", "GetOpenOpeningsReport", ReportQueries.OpenOpenings);
        Get(g, "/stage-funnel", "GetStageFunnelReport", (db, f, _, ct) => ReportQueries.StageFunnel(db, f, ct));
        Get(g, "/sources", "GetSourcesReport", (db, f, _, ct) => ReportQueries.Sources(db, f, ct));
        Get(g, "/client-openings", "GetClientOpeningsReport", (db, f, _, ct) => ReportQueries.ClientOpenings(db, f, ct));

        g.MapPost("/exports", async (ReportExportRequest r, DbContext db, RequestContext ctx, IClock clock, IReportRenderer renderer,
                IObjectStorage storage, CancellationToken ct) =>
            {
                var (found, problem) = await Filters(db, clock, r.ClientId, r.RecruiterId, r.From, r.To, ct);
                if (found is not { } filters)
                    return problem!;
                var document = await Document(db, r.Report, filters, clock, ct);
                var bytes = renderer.Render(document, r.Format);

                var now = clock.UtcNow;
                var key = $"recruiting-reports/{now:yyyy}/{now:MM}/{Guid.CreateVersion7()}.{r.Format.Extension()}";
                await storage.UploadAsync(StorageBucket.Exports, key, bytes, r.Format.ContentType(), ct);
                var fileName = r.Report == RecruitingReport.OpenOpenings
                    ? $"{document.Title} al {clock.Today():yyyy-MM-dd}.{r.Format.Extension()}"
                    : $"{document.Title} {filters.From:yyyy-MM-dd} a {filters.To:yyyy-MM-dd}.{r.Format.Extension()}";
                var download = await storage.PresignDownloadAsync(StorageBucket.Exports, key, fileName, DownloadLifetime, ct);

                db.Add(new AuditEntry
                {
                    OccurredAt = now,
                    UserId = ctx.RequiredPrincipal.UserId,
                    ActingForUserId = ctx.ActingFor?.UserId,
                    Action = AuditAction.Export,
                    SchemaName = "platform",
                    TableName = View(r.Report),
                    After = JsonSerializer.Serialize(new
                    {
                        report = r.Report.ToString(),
                        format = r.Format.ToString(),
                        clientId = filters.ClientId,
                        recruiterId = filters.RecruiterId,
                        from = filters.From,
                        to = filters.To,
                        key,
                    }),
                    RequestId = ctx.RequestId,
                });
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new ReportExportDto(download.Url, fileName, download.ExpiresAt));
            })
            .WithName("ExportRecruitingReport").Produces<ReportExportDto>().ProducesValidationProblem().RequireAccess(Access)
            .Invalidates(Tag);
        return v1;
    }

    private static void Get<T>(RouteGroupBuilder g, string path, string name,
        Func<DbContext, ReportFiltersDto, IClock, CancellationToken, Task<T>> query) =>
        g.MapGet(path, async (Guid? clientId, Guid? recruiterId, DateOnly? from, DateOnly? to, DbContext db, IClock clock,
                CancellationToken ct) =>
            {
                var (filters, problem) = await Filters(db, clock, clientId, recruiterId, from, to, ct);
                return problem ?? TypedResults.Ok(await query(db, filters!, clock, ct));
            })
            .WithName(name).Produces<T>().ProducesValidationProblem().RequireAccess(Access);

    private static async Task<(ReportFiltersDto? Filters, IResult? Problem)> Filters(DbContext db, IClock clock, Guid? clientId,
        Guid? recruiterId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var filters = await ReportQueries.Filters(db, clock, clientId, recruiterId, from, to, ct);
        if (filters is null)
            return (null, ApiResults.Validation(new Dictionary<string, string[]> { [clientId is null ? "recruiterId" : "clientId"] = [UnknownFilter] }));
        if (filters.To < filters.From)
            return (null, ApiResults.Validation(new Dictionary<string, string[]> { ["to"] = [RangeReversed] }));
        return (filters, null);
    }

    private static async Task<ReportDocument> Document(DbContext db, RecruitingReport report, ReportFiltersDto f, IClock clock,
        CancellationToken ct) => report switch
        {
            RecruitingReport.RecruiterPerformance => ReportDocuments.RecruiterPerformance(await ReportQueries.RecruiterPerformance(db, f, ct)),
            RecruitingReport.Summary => ReportDocuments.Summary(await ReportQueries.Summary(db, f, ct)),
            RecruitingReport.OpenOpenings => ReportDocuments.OpenOpenings(await ReportQueries.OpenOpenings(db, f, clock, ct)),
            RecruitingReport.StageFunnel => ReportDocuments.StageFunnel(await ReportQueries.StageFunnel(db, f, ct)),
            RecruitingReport.Sources => ReportDocuments.Sources(await ReportQueries.Sources(db, f, ct)),
            _ => ReportDocuments.ClientOpenings(await ReportQueries.ClientOpenings(db, f, ct)),
        };

    /// <summary>The view an export read, for its <c>audit_log</c> row.</summary>
    private static string View(RecruitingReport report) => report switch
    {
        RecruitingReport.RecruiterPerformance => "recruiter_performance",
        RecruitingReport.Summary => "client_openings",
        RecruitingReport.OpenOpenings => "open_openings_aging",
        RecruitingReport.StageFunnel => "stage_funnel",
        RecruitingReport.Sources => "source_effectiveness",
        _ => "client_openings",
    };
}
