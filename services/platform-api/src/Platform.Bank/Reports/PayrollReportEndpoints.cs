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
using Platform.Payroll;
using Platform.Payroll.Groups;
using Platform.Recruiting.Reports;

namespace Platform.Bank.Reports;

/// <summary>
/// Reportes de nómina (SPEC §5.9, PLAN D87): the pickers, one GET per report with Cliente and a period or a range, and the export
/// to Excel or PDF — grant <c>payroll_prepayroll</c>; a client-scoped grant sees only its clients.
/// </summary>
public static class PayrollReportEndpoints
{
    public const string Tag = "payroll-reports";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollPrepayroll);

    public const string UnknownReport = "Ese reporte no existe.";

    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(10);

    public static IEndpointRouteBuilder MapPayrollReportEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/payroll-reports").WithTags("PayrollReports");

        g.MapGet("/options", async (DbContext db, RequestContext ctx, CancellationToken ct) =>
                TypedResults.Ok(await PayrollReports.OptionsAsync(db, Groups(db, ctx), ct)))
            .WithName("GetPayrollReportOptions").RequireAccess(Access);

        g.MapGet("/{report}", async (string report, Guid? clientId, Guid? periodId, DateOnly? from, DateOnly? to, DbContext db, RequestContext ctx,
                IClock clock, CancellationToken ct) =>
            {
                if (!WireEnum.TryParse<PayrollReport>(report, out var which) || which is null)
                    return ApiResults.NotFound(UnknownReport);
                var (dto, field, problem) = await PayrollReports.BuildAsync(db, Groups(db, ctx), which.Value, clientId, periodId, from, to,
                    clock.Today(), ct);
                return dto is null
                    ? ApiResults.Validation(new Dictionary<string, string[]> { [field!] = [problem!] })
                    : TypedResults.Ok(dto);
            })
            .WithName("GetPayrollReport").Produces<PayrollReportDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(Access);

        g.MapPost("/exports", async (PayrollReportExportRequest r, DbContext db, RequestContext ctx, IClock clock, IReportRenderer renderer,
                IObjectStorage storage, CancellationToken ct) =>
            {
                var (dto, field, problem) = await PayrollReports.BuildAsync(db, Groups(db, ctx), r.Report, r.ClientId, r.PeriodId, r.From, r.To,
                    clock.Today(), ct);
                if (dto is null)
                    return ApiResults.Validation(new Dictionary<string, string[]> { [field!] = [problem!] });
                var bytes = renderer.Render(PayrollReports.Document(dto), r.Format);
                var now = clock.UtcNow;
                var key = $"payroll-reports/{now:yyyy}/{now:MM}/{Guid.CreateVersion7()}.{r.Format.Extension()}";
                await storage.UploadAsync(StorageBucket.Exports, key, bytes, r.Format.ContentType(), ct);
                var period = dto.Filters.From is { } f && dto.Filters.To is { } t ? $" {f:yyyy-MM-dd} a {t:yyyy-MM-dd}" : $" al {clock.Today():yyyy-MM-dd}";
                var fileName = $"{dto.Title}{(dto.Filters.ClientName is { } c ? $" {c}" : "")}{period}.{r.Format.Extension()}";
                var download = await storage.PresignDownloadAsync(StorageBucket.Exports, key, fileName, DownloadLifetime, ct);
                db.Add(new AuditEntry
                {
                    OccurredAt = now,
                    UserId = ctx.RequiredPrincipal.UserId,
                    ActingForUserId = ctx.ActingFor?.UserId,
                    Action = AuditAction.Export,
                    SchemaName = "platform",
                    TableName = "prepayroll_lines",
                    After = JsonSerializer.Serialize(new
                    {
                        report = r.Report.ToString(),
                        format = r.Format.ToString(),
                        clientId = dto.Filters.ClientId,
                        periodId = dto.Filters.PeriodId,
                        from = dto.Filters.From,
                        to = dto.Filters.To,
                        key,
                    }),
                    RequestId = ctx.RequestId,
                });
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new ReportExportDto(download.Url, fileName, download.ExpiresAt));
            })
            .WithName("ExportPayrollReport").Produces<ReportExportDto>().ProducesValidationProblem().RequireAccess(Access).Invalidates(Tag);

        return v1;
    }

    private static IQueryable<PayrollGroup> Groups(DbContext db, RequestContext ctx) =>
        PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll);
}
