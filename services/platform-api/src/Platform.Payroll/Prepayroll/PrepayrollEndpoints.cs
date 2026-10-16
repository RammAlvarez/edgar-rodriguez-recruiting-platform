using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Users;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Staff;

namespace Platform.Payroll.Prepayroll;

/// <summary>
/// Prenómina (SPEC §5.5, PLAN D84): periods, the grid in the client's layout, a row's desglose and comments, ajustes manuales
/// (D63) and «Cerrar captura» — grant <c>payroll_prepayroll</c>, limited to its clients. Incidencias are
/// <see cref="IncidentEndpoints"/>.
/// </summary>
public static partial class PrepayrollEndpoints
{
    public const string Tag = "prepayroll";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollPrepayroll);

    public const string PeriodNotFound = "El período no existe o no es de tus clientes.";
    public const string LineNotFound = "La línea no existe o no es de tus clientes.";
    public const string GroupNotFound = "El grupo no existe o no es de tus clientes.";
    public const string Paid = "El período ya se pagó: no cambia. Corrige con un retroactivo en el siguiente período.";
    public const string NotCapturing = "El período ya no está en captura.";
    public const string NoBankData = "Los comentarios no llevan datos bancarios (números de cuenta o tarjeta).";

    public static IEndpointRouteBuilder MapPrepayrollEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/payroll").WithTags("Prepayroll");

        g.MapGet("/periods", async (Guid? groupId, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var groups = Groups(db, ctx);
                var q = db.Set<PayrollPeriod>().AsQueryable();
                if (groupId is { } id)
                    q = q.Where(p => p.PayrollGroupId == id);
                return TypedResults.Ok(await PrepayrollQueries.Periods(db, groups, q, ct));
            })
            .WithName("ListPayrollPeriods").Produces<List<PayrollPeriodDto>>().RequireAccess(Access);

        // Periods are created ahead by period-roll; this one creates a past or missing one (the S9 seed, a late alta).
        g.MapPost("/groups/{id:guid}/periods", async (Guid id, CreatePeriodRequest r, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                var group = await Groups(db, ctx).AsNoTracking().SingleOrDefaultAsync(x => x.Uid == id, ct);
                if (group is null)
                    return ApiResults.NotFound(GroupNotFound);
                var (start, _, _, _) = PeriodCalendar.Containing(group, r.Date);
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await PeriodCalendar.EnsureAsync(db, group, [r.Date], ct);
                await db.SaveChangesAsync(ct);
                var period = await db.Set<PayrollPeriod>().SingleAsync(p => p.PayrollGroupId == id && p.StartDate == start, ct);
                await Prepayrolls.EnsureLinesAsync(db, period, clock.UtcNow, ct);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return TypedResults.Ok((await PrepayrollQueries.Periods(db, Groups(db, ctx), db.Set<PayrollPeriod>().Where(p => p.Uid == period.Uid), ct))
                    .Single());
            })
            .WithName("CreatePayrollPeriod").Produces<PayrollPeriodDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(Tag).RequireAccess(Access);

        g.MapGet("/periods/{id:guid}", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                await PrepayrollQueries.Grid(db, Groups(db, ctx), id, clock.Today(), ct) is { } grid
                    ? TypedResults.Ok(grid)
                    : ApiResults.NotFound(PeriodNotFound))
            .WithName("GetPrepayroll").Produces<PrepayrollGridDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Access);

        // «Cerrar captura» before the last day locks: the same move the attendance-lock job makes (SPEC §5.3).
        g.MapPost("/periods/{id:guid}/close-capture", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var period = await Period(db, ctx, id, ct);
                if (period is null)
                    return ApiResults.NotFound(PeriodNotFound);
                if (period.Status != PeriodStatus.Capturing)
                    return ApiResults.Conflict(NotCapturing);
                var group = await db.Set<PayrollGroup>().AsNoTracking().SingleAsync(x => x.Uid == period.PayrollGroupId, ct);
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await Prepayrolls.CloseCaptureAsync(db, group, period, clock.UtcNow, ct);
                await transaction.CommitAsync(ct);
                return TypedResults.Ok(await PrepayrollQueries.Grid(db, Groups(db, ctx), id, clock.Today(), ct));
            })
            .WithName("ClosePrepayrollCapture").Produces<PrepayrollGridDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, AttendanceEndpoints.Tag).RequireAccess(Access);

        g.MapGet("/lines/{id:guid}", async (Guid id, DbContext db, RequestContext ctx, CancellationToken ct) =>
                await PrepayrollQueries.Detail(db, Groups(db, ctx), id, ct) is { } line ? TypedResults.Ok(line) : ApiResults.NotFound(LineNotFound))
            .WithName("GetPrepayrollLine").Produces<PrepayrollLineDetailDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Access);

        g.MapPut("/lines/{id:guid}/comments", async (Guid id, LineCommentsRequest r, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var found = await Line(db, ctx, id, ct);
                if (found is not { } x)
                    return ApiResults.NotFound(LineNotFound);
                if (x.Period.Status == PeriodStatus.Paid)
                    return ApiResults.Conflict(Paid);
                var comments = r.Comments.Clean();
                if (comments is { Length: > 500 })
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["comments"] = ["Los comentarios admiten hasta 500 caracteres."] });
                if (comments is not null && LongNumber().IsMatch(comments))
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["comments"] = [NoBankData] });
                x.Line.Comments = comments;
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("SetPrepayrollLineComments").Produces(StatusCodes.Status204NoContent).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag).RequireAccess(Access);

        // «Editar celda» (D63): the difference becomes a signed MANUAL_ADJUSTMENT shown in that column, with its reason.
        g.MapPost("/lines/{id:guid}/adjustments", async (Guid id, AdjustLineRequest r, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                var found = await Line(db, ctx, id, ct);
                if (found is not { } x)
                    return ApiResults.NotFound(LineNotFound);
                if (x.Period.Status == PeriodStatus.Paid)
                    return ApiResults.Conflict(Paid);
                var errors = new Dictionary<string, string[]>();
                var concept = PrepayrollQueries.ConceptOfColumn(r.Column);
                if (concept is null)
                    errors["column"] = ["Esa columna no se ajusta."];
                if (r.Reason.Clean() is not { } reason)
                    errors["reason"] = ["Escribe el motivo del ajuste."];
                else if (reason.Length > 200)
                    errors["reason"] = ["El motivo admite hasta 200 caracteres."];
                if (decimal.Round(r.Amount, 2) != r.Amount || Math.Abs(r.Amount) >= 1_000_000m)
                    errors["amount"] = ["Escribe un importe con hasta dos decimales."];
                if (errors.Count > 0)
                    return ApiResults.Validation(errors);
                var current = (await PrepayrollQueries.ColumnTotals(db, x.Line, ct)).GetValueOrDefault(r.Column);
                var delta = r.Amount - current;
                if (delta == 0)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["amount"] = ["La celda ya tiene ese importe."] });
                var codes = await db.Set<PayConceptRow>().ToDictionaryAsync(c => c.Code, c => c.Uid, ct);
                var now = clock.UtcNow;
                db.Add(new Incident
                {
                    EmployeeId = x.Line.EmployeeId,
                    PayrollPeriodId = x.Period.Uid,
                    PayConceptId = codes[PayConcept.ManualAdjustment.Info().Code],
                    AdjustedConceptId = codes[concept!.Value.Info().Code],
                    IncidentDate = x.Period.EndDate,
                    Quantity = 1,
                    UnitValue = delta,
                    Amount = delta,
                    Reason = r.Reason.Clean(),
                    DuringReview = x.Period.Status == PeriodStatus.InReview,
                    ReportedBy = ctx.RequiredPrincipal.UserId,
                });
                await Prepayrolls.SaveAndRecomputeLinesAsync(db, [(x.Period.Uid, x.Line.EmployeeId)], now, ct);
                return TypedResults.Ok(await PrepayrollQueries.Detail(db, Groups(db, ctx), id, ct));
            })
            .WithName("AdjustPrepayrollLine").Produces<PrepayrollLineDetailDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .Invalidates(Tag, IncidentEndpoints.Tag).RequireAccess(Access);

        return v1;
    }

    public static IQueryable<PayrollGroup> Groups(DbContext db, RequestContext ctx) =>
        PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll);

    private static async Task<PayrollPeriod?> Period(DbContext db, RequestContext ctx, Guid id, CancellationToken ct)
    {
        var groups = Groups(db, ctx);
        return await db.Set<PayrollPeriod>().AsNoTracking().SingleOrDefaultAsync(p => p.Uid == id && groups.Any(g => g.Uid == p.PayrollGroupId), ct);
    }

    private static async Task<(PrepayrollLine Line, PayrollPeriod Period)?> Line(DbContext db, RequestContext ctx, Guid id, CancellationToken ct)
    {
        var line = await db.Set<PrepayrollLine>().Include(l => l.Items).SingleOrDefaultAsync(l => l.Uid == id, ct);
        if (line is null || await Period(db, ctx, line.PayrollPeriodId, ct) is not { } period)
            return null;
        return (line, period);
    }

    /// <summary>Ten digits or more in a row (spaces and dashes allowed): an account, card or CLABE number.</summary>
    [GeneratedRegex(@"\d(?:[\s-]?\d){9,}")]
    private static partial Regex LongNumber();
}
