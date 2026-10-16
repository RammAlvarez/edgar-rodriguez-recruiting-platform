using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Staff;

namespace Platform.Payroll.Prepayroll;

/// <summary>
/// Incidencias (SPEC §5.4, D84): what changes pay and is not attendance — horas extra (sencillas, dobles, triples), retroactivo,
/// descuento, abono a préstamo. Grant <c>payroll_incidents</c>. An incidencia is paid in the period containing its date; when
/// that period is paid it goes to the next open one, remembering where it belongs (retroactivo, DATA_MODEL §5.9). Descanso,
/// festivo and prima come from attendance only; ajustes manuales from the grid.
/// </summary>
public static class IncidentEndpoints
{
    public const string Tag = "incidents";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollIncidents);

    public const string NotFound = "La incidencia no existe o no es de tus clientes.";
    public const string PaidIncident = "La incidencia está en un período pagado: no cambia. Registra un retroactivo.";

    /// <summary>The concepts an incidencia may carry; the hour ones are priced from their hours at the line's rates.</summary>
    public static readonly IReadOnlyDictionary<string, bool> Concepts = new Dictionary<string, bool>
    {
        [PayConcept.Overtime.Info().Code] = true,
        [PayConcept.OvertimeDouble.Info().Code] = true,
        [PayConcept.OvertimeTriple.Info().Code] = true,
        [PayConcept.Retroactive.Info().Code] = false,
        [PayConcept.Deduction.Info().Code] = false,
        [PayConcept.LoanInstallment.Info().Code] = false,
    };

    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/payroll/incidents").WithTags("Incidents");

        g.MapGet("", async (Guid? periodId, Guid? employeeId, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var q = Scope(db, ctx);
                if (periodId is { } p)
                    q = q.Where(i => i.PayrollPeriodId == p || i.OriginPeriodId == p);
                if (employeeId is { } e)
                    q = q.Where(i => i.EmployeeId == e);
                return TypedResults.Ok(await PrepayrollQueries.Incidents(db, q.Take(500), ct));
            })
            .WithName("ListIncidents").Produces<List<IncidentDto>>().RequireAccess(Access);

        // The pickers of the Incidencias screen, in this grant's scope (an Incidencias user may not hold Prenómina).
        g.MapGet("/periods", async (DbContext db, RequestContext ctx, CancellationToken ct) =>
                TypedResults.Ok(await PrepayrollQueries.Periods(db, PayrollScope.Groups(db, ctx, PermissionModule.PayrollIncidents),
                    db.Set<PayrollPeriod>(), ct)))
            .WithName("ListIncidentPeriods").Produces<List<PayrollPeriodDto>>().RequireAccess(Access);

        g.MapGet("/employees", async (Guid periodId, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var groups = PayrollScope.Groups(db, ctx, PermissionModule.PayrollIncidents);
                var period = await db.Set<PayrollPeriod>().AsNoTracking()
                    .SingleOrDefaultAsync(p => p.Uid == periodId && groups.Any(x => x.Uid == p.PayrollGroupId), ct);
                if (period is null)
                    return ApiResults.NotFound(PrepayrollEndpoints.PeriodNotFound);
                return TypedResults.Ok(await db.Set<Employee>().AsNoTracking()
                    .Where(e => e.PayrollGroupId == period.PayrollGroupId && e.HiredAt <= period.EndDate
                        && (e.TerminatedAt == null || e.TerminatedAt > period.StartDate))
                    .Join(db.Set<Person>(), e => e.PersonId, p => p.Uid, (e, p) => new { e, p.FullName })
                    .Join(db.Set<Position>(), x => x.e.PositionId, p => p.Uid, (x, p) => new { x.e.Uid, x.FullName, Position = p.Name })
                    .OrderBy(x => x.FullName)
                    .Select(x => new EmployeePickDto(x.Uid, x.FullName, x.Position)).ToListAsync(ct));
            })
            .WithName("ListIncidentEmployees").Produces<List<EmployeePickDto>>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Access);

        g.MapPost("", async (IncidentRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var incident = new Incident { ReportedBy = ctx.RequiredPrincipal.UserId };
                return await Write(db, ctx, clock, incident, r, isNew: true, ct);
            })
            .WithName("CreateIncident").Produces<IncidentDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag).RequireAccess(Access);

        g.MapPut("/{id:guid}", async (Guid id, IncidentRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var incident = await Scope(db, ctx).SingleOrDefaultAsync(i => i.Uid == id, ct);
                if (incident is null)
                    return ApiResults.NotFound(NotFound);
                if (incident.AdjustedConceptId is not null)
                    return ApiResults.Conflict("Un ajuste manual se cambia desde la celda de la prenómina.");
                if (await IsPaid(db, incident.PayrollPeriodId, ct))
                    return ApiResults.Conflict(PaidIncident);
                return await Write(db, ctx, clock, incident, r, isNew: false, ct);
            })
            .WithName("UpdateIncident").Produces<IncidentDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag).RequireAccess(Access);

        // Also removes an ajuste manual (from the desglose).
        g.MapDelete("/{id:guid}", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var incident = await Scope(db, ctx).SingleOrDefaultAsync(i => i.Uid == id, ct)
                    ?? await AdjustmentInScope(db, ctx, id, ct);
                if (incident is null)
                    return ApiResults.NotFound(NotFound);
                if (await IsPaid(db, incident.PayrollPeriodId, ct))
                    return ApiResults.Conflict(PaidIncident);
                // The line's items point at it: they go first, then the line is recomputed without it.
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Set<PrepayrollLineItem>().Where(i => i.IncidentId == incident.Uid).ExecuteDeleteAsync(ct);
                db.Remove(incident);
                await Prepayrolls.SaveAndRecomputeLinesAsync(db, [(incident.PayrollPeriodId, incident.EmployeeId)], clock.UtcNow, ct);
                await transaction.CommitAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteIncident").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag).RequireAccess(Access);

        return v1;
    }

    private static IQueryable<Incident> Scope(DbContext db, RequestContext ctx)
    {
        var employees = PayrollScope.Employees(db, ctx, PermissionModule.PayrollIncidents);
        return db.Set<Incident>().Where(i => i.AdjustedConceptId == null && employees.Any(e => e.Uid == i.EmployeeId));
    }

    /// <summary>An ajuste manual is the grid's: removable by whoever may edit the prenómina of its client.</summary>
    private static Task<Incident?> AdjustmentInScope(DbContext db, RequestContext ctx, Guid id, CancellationToken ct)
    {
        if (!ctx.RequiredPrincipal.HasGrant(PermissionModule.PayrollPrepayroll))
            return Task.FromResult<Incident?>(null);
        var employees = PayrollScope.Employees(db, ctx, PermissionModule.PayrollPrepayroll);
        return db.Set<Incident>().SingleOrDefaultAsync(i => i.Uid == id && i.AdjustedConceptId != null && employees.Any(e => e.Uid == i.EmployeeId), ct);
    }

    private static Task<bool> IsPaid(DbContext db, Guid periodId, CancellationToken ct) =>
        db.Set<PayrollPeriod>().AnyAsync(p => p.Uid == periodId && p.Status == PeriodStatus.Paid, ct);

    private static async Task<IResult> Write(DbContext db, RequestContext ctx, IClock clock, Incident incident, IncidentRequest r, bool isNew,
        CancellationToken ct)
    {
        var employee = await PayrollScope.Employees(db, ctx, PermissionModule.PayrollIncidents).AsNoTracking()
            .SingleOrDefaultAsync(e => e.Uid == r.EmployeeId, ct);
        if (employee is null)
            return ApiResults.NotFound(EmployeeEndpoints.NotFound);
        var errors = new Dictionary<string, string[]>();
        var code = r.ConceptCode.Clean()?.ToUpperInvariant() ?? "";
        if (!Concepts.TryGetValue(code, out var byHours))
            errors["conceptCode"] = ["Elige el tipo de incidencia."];
        else if (byHours && r.Quantity is not (> 0 and <= 168) || byHours && decimal.Round(r.Quantity!.Value, 2) != r.Quantity)
            errors["quantity"] = ["Escribe las horas (más de 0, hasta 168, con hasta dos decimales)."];
        else if (!byHours && (r.Amount is not (> 0 and < 1_000_000m) || decimal.Round(r.Amount!.Value, 2) != r.Amount))
            errors["amount"] = ["Escribe un importe mayor que cero con hasta dos decimales."];
        if (r.Reason.Clean() is { Length: > 200 })
            errors["reason"] = ["El motivo admite hasta 200 caracteres."];
        if (r.Date < employee.HiredAt || employee.TerminatedAt is { } t && r.Date >= t)
            errors["date"] = ["Esa fecha no está dentro del empleo de la persona."];
        if (errors.Count > 0)
            return ApiResults.Validation(errors);

        var group = await db.Set<PayrollGroup>().AsNoTracking().SingleAsync(x => x.Uid == employee.PayrollGroupId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (target, origin) = await RouteAsync(db, group, r.Date, ct);
        if (target.EndDate < employee.HiredAt || employee.TerminatedAt is { } end && end <= target.StartDate)
            return ApiResults.Conflict($"El período abierto (semana {target.PeriodNumber}) ya no incluye a la persona: no se le puede pagar ahí.");

        var concepts = await db.Set<PayConceptRow>().ToDictionaryAsync(c => c.Code, c => c.Uid, ct);
        var before = isNew ? null : ((Guid, Guid)?)(incident.PayrollPeriodId, incident.EmployeeId);
        incident.EmployeeId = employee.Uid;
        incident.PayrollPeriodId = target.Uid;
        incident.OriginPeriodId = origin?.Uid;
        incident.PayConceptId = concepts[code];
        incident.IncidentDate = r.Date;
        incident.Quantity = byHours ? r.Quantity!.Value : 1;
        incident.Amount = byHours ? 0 : r.Amount!.Value;
        incident.UnitValue = byHours ? 0 : r.Amount!.Value;
        incident.Reason = r.Reason.Clean();
        incident.DuringReview = target.Status == PeriodStatus.InReview;
        if (isNew)
            db.Add(incident);
        var lines = new List<(Guid, Guid)> { (target.Uid, employee.Uid) };
        if (before is { } b)
            lines.Add(b);
        await Prepayrolls.SaveAndRecomputeLinesAsync(db, lines, clock.UtcNow, ct);
        await transaction.CommitAsync(ct);
        return TypedResults.Ok((await PrepayrollQueries.Incidents(db, db.Set<Incident>().Where(i => i.Uid == incident.Uid), ct)).Single());
    }

    /// <summary>
    /// The period an incidencia dated <paramref name="date"/> is paid in: the one containing the date, or — when that one is paid —
    /// the next open one (created if missing), with the paid one as its origin.
    /// </summary>
    public static async Task<(PayrollPeriod Target, PayrollPeriod? Origin)> RouteAsync(DbContext db, PayrollGroup group, DateOnly date,
        CancellationToken ct)
    {
        PayrollPeriod? origin = null;
        var day = date;
        while (true)
        {
            await PeriodCalendar.EnsureAsync(db, group, [day], ct);
            await db.SaveChangesAsync(ct);
            var (start, _, _, _) = PeriodCalendar.Containing(group, day);
            var period = await db.Set<PayrollPeriod>().AsNoTracking().SingleAsync(p => p.PayrollGroupId == group.Uid && p.StartDate == start, ct);
            if (period.Status != PeriodStatus.Paid)
                return (period, origin);
            origin ??= period;
            day = period.EndDate.AddDays(1);
        }
    }
}
