using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.Persistence;
using Platform.Core.Users;
using Platform.Payroll.Engine;

namespace Platform.Payroll.Groups;

/// <summary>
/// Grupos de nómina, puestos and their rates (SPEC §5.6: rates live only in Clientes (nómina) → client detail). Grant
/// <c>payroll_prepayroll</c>, limited to its clients when the grant is client-scoped (DATA_MODEL §8).
/// </summary>
public static class GroupEndpoints
{
    public const string Tag = "payroll-groups";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollPrepayroll);

    public const string NotFound = "El grupo de nómina no existe.";
    public const string PositionNotFound = "El puesto no existe.";
    public const string NameTaken = "El cliente ya tiene un grupo con ese nombre.";
    public const string PositionTaken = "El grupo ya tiene un puesto con ese nombre.";
    public const string ClientNotFound = "El cliente no existe.";
    public static string RatesOutOfOrder(DateOnly current) =>
        $"La fecha debe ser posterior al inicio de las tarifas vigentes ({current.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}).";

    /// <summary>The concepts a group switches on or prices (D63); the rest always apply as the engine defines them.</summary>
    public static readonly IReadOnlyList<PayConcept> Configurable =
        [PayConcept.BonusDaysWorked, PayConcept.BonusPunctuality, PayConcept.BonusProductivity, PayConcept.RestDayWorked];

    public static IEndpointRouteBuilder MapPayrollGroupEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/payroll/groups").WithTags("Payroll groups");

        g.MapGet("", async (Guid? clientId, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var q = PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll);
                if (clientId is { } c)
                    q = q.Where(x => x.ClientId == c);
                return TypedResults.Ok(await List(db, q, ct));
            })
            .WithName("ListPayrollGroups").RequireAccess(Access);

        g.MapGet("/{id:guid}", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                await Detail(db, PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll), id, clock.Today(), ct) is { } dto
                    ? TypedResults.Ok(dto)
                    : ApiResults.NotFound(NotFound))
            .WithName("GetPayrollGroup").Produces<PayrollGroupDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(Access);

        g.MapPost("", async (PayrollGroupRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (!PayrollScope.InScope(ctx, PermissionModule.PayrollPrepayroll, r.ClientId)
                    || await db.Set<Client>().SingleOrDefaultAsync(c => c.Uid == r.ClientId, ct) is not { } client)
                    return ApiResults.NotFound(ClientNotFound);
                var group = new PayrollGroup { ClientId = r.ClientId, Name = "" };
                Apply(group, r);
                db.Add(group);
                // A group makes the client a payroll client (it shows in Clientes (nómina), SPEC §3.3).
                client.UsesPayroll = true;
                foreach (var concept in await db.Set<PayConceptRow>().Where(c => ConfigurableCodes.Contains(c.Code)).ToListAsync(ct))
                    db.Add(new PayrollGroupConcept { PayrollGroupId = group.Uid, PayConceptId = concept.Uid, IsActive = false });
                return await db.SaveOrConflictAsync(async () => Result(await Detail(db, db.Set<PayrollGroup>(), group.Uid, clock.Today(), ct)),
                    NameTaken, ct);
            })
            .WithName("CreatePayrollGroup").Mutation<PayrollGroupRequest>().RequireAccess(Access);

        g.MapPut("/{id:guid}", async (Guid id, PayrollGroupRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll).SingleOrDefaultAsync(x => x.Uid == id, ct) is not { } group)
                    return ApiResults.NotFound(NotFound);
                Apply(group, r);
                return await db.SaveOrConflictAsync(async () =>
                {
                    // The group's rules (séptimo día, prima, festivo) are in every open line.
                    await Prepayroll.Prepayrolls.RecomputeGroupAsync(db, id, clock.UtcNow, ct);
                    await db.SaveChangesAsync(ct);
                    return Result(await Detail(db, db.Set<PayrollGroup>(), id, clock.Today(), ct));
                }, NameTaken, ct);
            })
            .WithName("UpdatePayrollGroup").Mutation<PayrollGroupRequest>().RequireAccess(Access);

        g.MapPost("/{id:guid}/positions", async (Guid id, PositionRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (!await PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll).AnyAsync(x => x.Uid == id, ct))
                    return ApiResults.NotFound(NotFound);
                var position = new Position { PayrollGroupId = id, Name = PositionName(r.Name) };
                db.Add(position);
                db.Add(new PositionRate
                {
                    PositionId = position.Uid,
                    DailySalaryPocket = r.DailySalaryPocket,
                    DailySalaryImss = r.DailySalaryImss,
                    HourlyRate = r.HourlyRate,
                    DaysWorkedBonus = r.DaysWorkedBonus,
                    ValidFrom = r.ValidFrom ?? clock.Today(),
                });
                return await db.SaveOrConflictAsync(async () => Result(await Detail(db, db.Set<PayrollGroup>(), id, clock.Today(), ct)),
                    PositionTaken, ct);
            })
            .WithName("CreatePosition").Mutation<PositionRequest>().RequireAccess(Access);

        g.MapPut("/{id:guid}/concepts", async (Guid id, GroupConceptsRequest r, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                if (!await PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll).AnyAsync(x => x.Uid == id, ct))
                    return ApiResults.NotFound(NotFound);
                var concepts = await db.Set<PayConceptRow>().Where(c => ConfigurableCodes.Contains(c.Code)).ToDictionaryAsync(c => c.Code, ct);
                var rows = await db.Set<PayrollGroupConcept>().Where(c => c.PayrollGroupId == id).ToDictionaryAsync(c => c.PayConceptId, ct);
                foreach (var item in r.Concepts)
                {
                    if (!concepts.TryGetValue(item.Code, out var concept))
                        return ApiResults.Validation(new Dictionary<string, string[]> { ["concepts"] = [$"Concepto no configurable: {item.Code}."] });
                    if (!rows.TryGetValue(concept.Uid, out var row))
                        db.Add(row = new PayrollGroupConcept { PayrollGroupId = id, PayConceptId = concept.Uid });
                    row.IsActive = item.IsActive;
                    row.AmountOverride = item.Amount;
                    row.RateOverride = concept.Calc == ConceptCalc.PerDayWorked ? item.DaysBasis : null;
                }
                await db.SaveChangesAsync(ct);
                await Prepayroll.Prepayrolls.RecomputeGroupAsync(db, id, clock.UtcNow, ct);
                await db.SaveChangesAsync(ct);
                return Result(await Detail(db, db.Set<PayrollGroup>(), id, clock.Today(), ct));
            })
            .WithName("UpdatePayrollGroupConcepts").Mutation<GroupConceptsRequest>().RequireAccess(Access);

        var p = v1.MapGroup("/payroll/positions").WithTags("Payroll groups");

        p.MapPut("/{id:guid}", async (Guid id, UpdatePositionRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Position(db, ctx, id, ct) is not { } position)
                    return ApiResults.NotFound(PositionNotFound);
                position.Name = PositionName(r.Name);
                position.IsActive = r.IsActive;
                return await db.SaveOrConflictAsync(
                    async () => Result(await Detail(db, db.Set<PayrollGroup>(), position.PayrollGroupId, clock.Today(), ct)), PositionTaken, ct);
            })
            .WithName("UpdatePosition").Mutation<UpdatePositionRequest>().RequireAccess(Access);

        p.MapPost("/{id:guid}/rates", async (Guid id, PositionRatesRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Position(db, ctx, id, ct) is not { } position)
                    return ApiResults.NotFound(PositionNotFound);
                var open = await db.Set<PositionRate>().SingleOrDefaultAsync(x => x.PositionId == id && x.ValidTo == null, ct);
                if (open is not null && r.ValidFrom <= open.ValidFrom)
                    return ApiResults.Validation(new Dictionary<string, string[]>
                    {
                        ["validFrom"] = [RatesOutOfOrder(open.ValidFrom)],
                    });
                if (open is not null)
                {
                    open.ValidTo = r.ValidFrom.AddDays(-1);
                    // The partial unique index allows one open row: close it before the new one is inserted.
                    await db.SaveChangesAsync(ct);
                }
                db.Add(new PositionRate
                {
                    PositionId = id,
                    DailySalaryPocket = r.DailySalaryPocket,
                    DailySalaryImss = r.DailySalaryImss,
                    HourlyRate = r.HourlyRate,
                    DaysWorkedBonus = r.DaysWorkedBonus,
                    ValidFrom = r.ValidFrom,
                });
                await db.SaveChangesAsync(ct);
                // A period uses the rates valid at its end: only open periods change, a paid one keeps what it paid.
                await Prepayroll.Prepayrolls.RecomputeGroupAsync(db, position.PayrollGroupId, clock.UtcNow, ct);
                await db.SaveChangesAsync(ct);
                return Result(await Detail(db, db.Set<PayrollGroup>(), position.PayrollGroupId, clock.Today(), ct));
            })
            .WithName("ChangePositionRates").Mutation<PositionRatesRequest>().RequireAccess(Access);
        return v1;
    }

    private static readonly string[] ConfigurableCodes = Configurable.Select(c => c.Info().Code).ToArray();

    private static RouteHandlerBuilder Mutation<TBody>(this RouteHandlerBuilder route) where TBody : class =>
        route.Validate<TBody>().Produces<PayrollGroupDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, ClientEndpoints.Tag, Prepayroll.PrepayrollEndpoints.Tag);

    private static IResult Result(PayrollGroupDetailDto? dto) => dto is null ? ApiResults.NotFound(NotFound) : TypedResults.Ok(dto);

    private static Task<Position?> Position(DbContext db, RequestContext ctx, Guid id, CancellationToken ct) =>
        db.Set<Position>().Where(x => x.Uid == id
                && PayrollScope.Groups(db, ctx, PermissionModule.PayrollPrepayroll).Any(g => g.Uid == x.PayrollGroupId))
            .SingleOrDefaultAsync(ct);

    private static void Apply(PayrollGroup g, PayrollGroupRequest r)
    {
        g.Name = PositionName(r.Name);
        g.PeriodType = r.PeriodType;
        g.CutoffWeekday = r.CutoffWeekday;
        g.ShiftHours = r.ShiftHours;
        g.AttendanceLockTime = r.AttendanceLockTime;
        g.SeventhDayDivisor = r.SeventhDayDivisor;
        g.SundayPremiumPct = r.SundayPremiumPct;
        g.HolidayWorkedMultiplier = r.HolidayWorkedMultiplier;
        g.ScheduleDescription = r.ScheduleDescription.Clean()?.ToUpperInvariant();
        g.TimeZone = r.TimeZone.Clean() ?? ClockExtensions.DefaultTimeZone;
        g.IsActive = r.IsActive;
    }

    /// <summary>Group and puesto names as the Excel writes them: uppercase, single spaces («VALIDADORES 12 HRS»).</summary>
    private static string PositionName(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static async Task<List<PayrollGroupListItemDto>> List(DbContext db, IQueryable<PayrollGroup> q, CancellationToken ct)
    {
        var rows = await q.Join(db.Set<Client>(), g => g.ClientId, c => c.Uid, (g, c) => new { g, Client = c.ShortName })
            .Select(x => new
            {
                x.g,
                x.Client,
                Positions = db.Set<Position>().Count(p => p.PayrollGroupId == x.g.Uid && p.IsActive),
                Employees = db.Set<Staff.Employee>().Count(e => e.PayrollGroupId == x.g.Uid && e.TerminatedAt == null),
            })
            .OrderBy(x => x.Client).ThenBy(x => x.g.Name).AsNoTracking().ToListAsync(ct);
        return rows.Select(x => new PayrollGroupListItemDto(x.g.Uid, x.g.ClientId, x.Client, x.g.Name, x.g.PeriodType, x.g.CutoffWeekday,
            x.g.ShiftHours, x.g.AttendanceLockTime, x.g.IsActive, x.Positions, x.Employees)).ToList();
    }

    public static async Task<PayrollGroupDetailDto?> Detail(DbContext db, IQueryable<PayrollGroup> scope, Guid id, DateOnly today,
        CancellationToken ct)
    {
        var x = await scope.Where(g => g.Uid == id).Join(db.Set<Client>(), g => g.ClientId, c => c.Uid, (g, c) => new { g, c.ShortName })
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (x is null)
            return null;
        var g = x.g;
        var positions = await db.Set<Position>().Where(p => p.PayrollGroupId == id).AsNoTracking().ToListAsync(ct);
        var positionIds = positions.Select(p => p.Uid).ToList();
        var rates = await db.Set<PositionRate>().Where(r => positionIds.Contains(r.PositionId)).AsNoTracking().ToListAsync(ct);
        var concepts = await db.Set<PayConceptRow>().Where(c => ConfigurableCodes.Contains(c.Code))
            .GroupJoin(db.Set<PayrollGroupConcept>().Where(c => c.PayrollGroupId == id), c => c.Uid, gc => gc.PayConceptId,
                (c, gcs) => new { c, gc = gcs.FirstOrDefault() })
            .AsNoTracking().ToListAsync(ct);
        return new PayrollGroupDetailDto(g.Uid, g.ClientId, x.ShortName, g.Name, g.PeriodType, g.CutoffWeekday, g.ShiftHours,
            g.AttendanceLockTime, g.SeventhDayDivisor, g.SundayPremiumPct, g.HolidayWorkedMultiplier, g.ScheduleDescription, g.TimeZone,
            g.IsActive,
            positions.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name, StringComparer.Ordinal).Select(p =>
                {
                    var history = rates.Where(r => r.PositionId == p.Uid).OrderByDescending(r => r.ValidFrom).Select(RateDto).ToList();
                    var current = history.FirstOrDefault(r => r.ValidFrom <= today && (r.ValidTo is null || r.ValidTo >= today));
                    return new PositionDto(p.Uid, p.Name, p.IsActive, current, history);
                })
                .ToList(),
            concepts.OrderBy(c => c.c.SortOrder).Select(c => new GroupConceptDto(c.c.Code, c.c.NameEs, c.c.Calc, c.gc?.IsActive ?? false,
                c.gc?.AmountOverride ?? c.c.DefaultAmount,
                c.c.Calc == ConceptCalc.PerDayWorked ? c.gc?.RateOverride ?? c.c.DefaultRate : null,
                c.c.Code == PayConcept.BonusDaysWorked.Info().Code && c.gc?.AmountOverride is null)).ToList());
    }

    private static PositionRateDto RateDto(PositionRate r) =>
        new(r.Uid, r.DailySalaryPocket, r.DailySalaryImss, r.HourlyRate, r.DaysWorkedBonus, r.ValidFrom, r.ValidTo);
}
