using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Persistence;
using Platform.Core.Security;
using Platform.Core.Users;
using Platform.Payroll.Groups;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Payroll.Staff;

/// <summary>
/// Personal (SPEC §5.1, §5.8): alta (direct or «Convertir en empleado»), edit, cambio de cliente / puesto, Cambiar estatus,
/// reingreso, «Solicitar tarjeta» and the Cuentas de pago tab. Grant <c>payroll_staff</c>, limited to the clients of a
/// client-scoped grant through the employee's group (DATA_MODEL §8).
/// </summary>
public static class EmployeeEndpoints
{
    public const string Tag = "employees";
    public const string CardRequestsTag = "card-requests";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollStaff);
    public static readonly AccessRule CardRequestAccess = AccessRule.Administrator(PermissionModule.PayrollCardRequests);

    public const string NotFound = "El empleado no existe o no es de tus clientes.";
    public const string GroupNotFound = "Elige un grupo de nómina de tus clientes.";
    public const string PositionNotInGroup = "Elige un puesto activo de ese grupo.";
    public const string PersonNotFound = "La persona no existe.";
    public const string AlreadyEmployed = "La persona ya trabaja en ese grupo de nómina.";
    public const string CardBankRequired = "La persona no tiene cuenta principal: elige el banco para solicitar su tarjeta.";
    public const string CandidateNotHired = "Solo un candidato Contratado se convierte en empleado.";
    public const string CandidateConverted = "Este candidato ya se convirtió en empleado.";
    public const string PeriodNotFound = "El periodo no existe.";
    public const string HasAccountAlready = "El empleado ya tiene cuenta principal; no necesita tarjeta.";
    public const string CardAlreadyRequested = "Ya hay una solicitud de tarjeta en curso.";
    public const string NotTerminated = "Solo un empleado dado de baja tiene reingreso.";

    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/employees").WithTags("Employees");

        g.MapGet("/options", async (DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                TypedResults.Ok(new EmployeeOptionsDto(await GroupOptions(db, ctx, clock.Today(), ct), StaffRules.Shifts, StaffRules.TerminationReasons)))
            .WithName("GetEmployeeOptions").RequireAccess(Access);

        g.MapGet("/conversion/{candidateId:guid}", async (Guid candidateId, DbContext db, IClock clock, CancellationToken ct) =>
                await Conversion(db, candidateId, clock.Today(), ct) is { } dto ? TypedResults.Ok(dto) : ApiResults.NotFound(CandidateNotHired))
            .WithName("GetEmployeeConversion").Produces<ConversionDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Access);

        g.MapGet("", async (Guid? clientId, Guid? groupId, Guid? positionId, string? status, bool? includeTerminated, DbContext db,
                RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (!WireEnum.TryParse<EmployeeStatus>(status, out var st))
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["status"] = ["Filtro desconocido."] });
                var q = PayrollScope.Employees(db, ctx, PermissionModule.PayrollStaff);
                if (clientId is { } c)
                    q = q.Where(e => db.Set<PayrollGroup>().Any(x => x.Uid == e.PayrollGroupId && x.ClientId == c));
                if (groupId is { } gid)
                    q = q.Where(e => e.PayrollGroupId == gid);
                if (positionId is { } pid)
                    q = q.Where(e => e.PositionId == pid);
                if (includeTerminated != true && st != EmployeeStatus.Terminated)
                    q = q.Where(e => e.TerminatedAt == null);
                var rows = await EmployeeQueries.List(db, q, clock.Today(), ct);
                return TypedResults.Ok(st is { } s ? rows.Where(r => r.Status == s).ToList() : rows);
            })
            .WithName("ListEmployees").Produces<List<EmployeeListItemDto>>().ProducesValidationProblem().RequireAccess(Access);

        g.MapGet("/{id:guid}", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                await EmployeeQueries.Detail(db, PayrollScope.Employees(db, ctx, PermissionModule.PayrollStaff), id, clock.Today(), ct) is { } dto
                    ? TypedResults.Ok(dto)
                    : ApiResults.NotFound(NotFound))
            .WithName("GetEmployee").Produces<EmployeeDetailDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Access);

        g.MapPost("", async (EmployeeRequest r, DbContext db, RequestContext ctx, IFieldCipher cipher, IClock clock, CancellationToken ct) =>
            {
                var today = clock.Today();
                if (await Assignment(db, ctx, r.PayrollGroupId, r.PositionId, ct) is { } bad)
                    return bad;
                if (!await db.Set<Person>().AnyAsync(p => p.Uid == r.PersonId && p.DeletedAt == null, ct))
                    return ApiResults.NotFound(PersonNotFound);
                var hiredAt = r.HiredAt;
                Candidate? candidate = null;
                if (r.CandidateId is { } cid)
                {
                    candidate = await db.Set<Candidate>().SingleOrDefaultAsync(c => c.Uid == cid && c.PersonId == r.PersonId, ct);
                    if (candidate is null || candidate.Stage != CandidateStage.Hired)
                        return ApiResults.Conflict(CandidateNotHired);
                    if (candidate.EmployeeId is not null)
                        return ApiResults.Conflict(CandidateConverted);
                    hiredAt ??= candidate.HiredAt;
                }
                if (await db.Set<Employee>().AnyAsync(e => e.PersonId == r.PersonId && e.PayrollGroupId == r.PayrollGroupId && e.TerminatedAt == null, ct))
                    return ApiResults.Conflict(AlreadyEmployed);
                var hasAccount = await StaffRules.HasAccountAsync(db, r.PersonId, today, ct);
                if (r.BankAccount is { } account)
                {
                    var person = await db.Set<Person>().SingleAsync(p => p.Uid == r.PersonId, ct);
                    var (_, errors) = await BankAccounts.AddAsync(db, cipher, person, account with { MakePrimary = true }, today, ct);
                    if (errors is not null)
                        return ApiResults.Validation(errors.ToDictionary(e => $"bankAccount.{e.Key}", e => e.Value));
                    hasAccount = true;
                }
                if (!hasAccount && r.CardBank is null)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["cardBank"] = [CardBankRequired] });

                var employee = new Employee
                {
                    PersonId = r.PersonId,
                    PayrollGroupId = r.PayrollGroupId,
                    PositionId = r.PositionId,
                    Shift = Shift(r.Shift),
                    HiredAt = hiredAt ?? today,
                    DailySalaryPocketOverride = r.DailySalaryPocketOverride,
                    DailySalaryImssOverride = r.DailySalaryImssOverride,
                    HourlyRateOverride = r.HourlyRateOverride,
                    DaysWorkedBonusOverride = r.DaysWorkedBonusOverride,
                    Notes = r.Notes.Clean(),
                    CandidateId = r.CandidateId,
                };
                db.Add(employee);
                if (candidate is not null)
                    candidate.EmployeeId = employee.Uid;
                // An employee created without an account is queued for a card automatically (SPEC §5.5).
                if (!hasAccount)
                    db.Add(new CardRequest { EmployeeId = employee.Uid, Bank = r.CardBank!.Value, RequestedAt = clock.UtcNow });
                return await db.SaveOrConflictAsync(async () => Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), employee.Uid, today, ct)),
                    AlreadyEmployed, ct);
            })
            .WithName("CreateEmployee").Mutation<EmployeeRequest>(CandidateEndpoints.Tag).RequireAccess(Access);

        g.MapPut("/{id:guid}", async (Guid id, UpdateEmployeeRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                e.Shift = Shift(r.Shift);
                e.DailySalaryPocketOverride = r.DailySalaryPocketOverride;
                e.DailySalaryImssOverride = r.DailySalaryImssOverride;
                e.HourlyRateOverride = r.HourlyRateOverride;
                e.DaysWorkedBonusOverride = r.DaysWorkedBonusOverride;
                e.Notes = r.Notes.Clean();
                await db.SaveChangesAsync(ct);
                return Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), id, clock.Today(), ct));
            })
            .WithName("UpdateEmployee").Mutation<UpdateEmployeeRequest>().RequireAccess(Access);

        g.MapPost("/{id:guid}/assignment", async (Guid id, AssignmentRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                if (e.TerminatedAt is not null)
                    return ApiResults.Conflict(StaffRules.AlreadyTerminated);
                if (await Assignment(db, ctx, r.PayrollGroupId, r.PositionId, ct) is { } bad)
                    return bad;
                if (r.PayrollGroupId != e.PayrollGroupId && await db.Set<Employee>().AnyAsync(x =>
                        x.PersonId == e.PersonId && x.PayrollGroupId == r.PayrollGroupId && x.TerminatedAt == null, ct))
                    return ApiResults.Conflict(AlreadyEmployed);
                e.PayrollGroupId = r.PayrollGroupId;
                e.PositionId = r.PositionId;
                e.Shift = Shift(r.Shift);
                return await db.SaveOrConflictAsync(async () => Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), id, clock.Today(), ct)),
                    AlreadyEmployed, ct);
            })
            .WithName("ChangeEmployeeAssignment").Mutation<AssignmentRequest>().RequireAccess(Access);

        MapStatus(g);
        MapCards(v1, g);
        MapAccounts(g);
        return v1;
    }

    // ---- Cambiar estatus (SPEC §5.1, DATA_MODEL §5.4b) ----

    private static void MapStatus(RouteGroupBuilder g)
    {
        g.MapPost("/{id:guid}/status", async (Guid id, StatusPeriodRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                if (e.TerminatedAt is not null)
                    return ApiResults.Conflict(StaffRules.AlreadyTerminated);
                if (r.StartDate < e.HiredAt)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["startDate"] = [StaffRules.BeforeHire(e.HiredAt)] });
                var end = r.Status == EmployeeStatus.Terminated ? null : r.EndDate;
                if (await Overlap(db, id, null, r.StartDate, end, ct) is { } other)
                    return ApiResults.Conflict(StaffRules.Overlaps(other));

                var period = new EmployeeStatusPeriod
                {
                    EmployeeId = id,
                    Status = r.Status,
                    StartDate = r.StartDate,
                    EndDate = end,
                    Reason = r.Reason.Clean(),
                    Reference = r.Reference.Clean()?.ToUpperInvariant(),
                };
                db.Add(period);
                if (r.Status == EmployeeStatus.Terminated)
                {
                    e.TerminatedAt = r.StartDate;
                    await StaffRules.CancelQueuedCardsAsync(db, db.Set<Employee>().Where(x => x.Uid == id).Select(x => x.Uid), ct);
                }
                await StaffRules.PrefillAsync(db, period, r.StartDate, end ?? r.StartDate, ctx.RequiredPrincipal.UserId, clock.UtcNow, ct);
                await RefreshStatus(db, e, clock.Today(), ct);
                return await db.SaveOrConflictAsync(async () => Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), id, clock.Today(), ct)),
                    "El periodo se cruza con otro.", ct);
            })
            .WithName("AddEmployeeStatusPeriod").Mutation<StatusPeriodRequest>(CardRequestsTag).RequireAccess(Access);

        g.MapPut("/{id:guid}/status/{periodId:guid}", async (Guid id, Guid periodId, UpdateStatusPeriodRequest r, DbContext db,
                RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                if (await db.Set<EmployeeStatusPeriod>().SingleOrDefaultAsync(p => p.Uid == periodId && p.EmployeeId == id, ct) is not { } period)
                    return ApiResults.NotFound(PeriodNotFound);
                if (period.Status == EmployeeStatus.Terminated)
                    return ApiResults.Conflict(StaffRules.BajaIsReingreso);
                if (r.EndDate < period.StartDate)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["endDate"] = ["La fecha de fin es anterior a la de inicio."] });
                if (await Overlap(db, id, periodId, period.StartDate, r.EndDate, ct) is { } other)
                    return ApiResults.Conflict(StaffRules.Overlaps(other));
                var previous = period.EndDate!.Value;
                period.EndDate = r.EndDate;
                period.Reference = r.Reference.Clean()?.ToUpperInvariant() ?? period.Reference;
                if (r.EndDate < previous)
                    await StaffRules.UnfillAsync(db, period, r.EndDate, ct);
                else
                    await StaffRules.PrefillAsync(db, period, previous.AddDays(1), r.EndDate, ctx.RequiredPrincipal.UserId, clock.UtcNow, ct);
                await RefreshStatus(db, e, clock.Today(), ct);
                await db.SaveChangesAsync(ct);
                return Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), id, clock.Today(), ct));
            })
            .WithName("UpdateEmployeeStatusPeriod").Mutation<UpdateStatusPeriodRequest>().RequireAccess(Access);

        // «Reactivar»: the period ends yesterday (the employee is back today); one that has not started is cancelled.
        g.MapPost("/{id:guid}/status/{periodId:guid}/end", async (Guid id, Guid periodId, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                if (await db.Set<EmployeeStatusPeriod>().SingleOrDefaultAsync(p => p.Uid == periodId && p.EmployeeId == id, ct) is not { } period)
                    return ApiResults.NotFound(PeriodNotFound);
                if (period.Status == EmployeeStatus.Terminated)
                    return ApiResults.Conflict(StaffRules.BajaIsReingreso);
                var today = clock.Today();
                if (period.StartDate >= today)
                {
                    await StaffRules.UnfillAsync(db, period, null, ct);
                    db.Remove(period);
                }
                else if (period.EndDate >= today)
                {
                    period.EndDate = today.AddDays(-1);
                    period.EndedEarlyAt = clock.UtcNow;
                    await StaffRules.UnfillAsync(db, period, period.EndDate, ct);
                }
                await RefreshStatus(db, e, today, ct, removed: period.StartDate >= today ? period.Uid : null);
                await db.SaveChangesAsync(ct);
                return Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), id, today, ct));
            })
            .WithName("EndEmployeeStatusPeriod").Produces<EmployeeDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag).RequireAccess(Access);

        g.MapPost("/{id:guid}/rehire", async (Guid id, RehireRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } previous)
                    return ApiResults.NotFound(NotFound);
                if (previous.TerminatedAt is not { } terminated)
                    return ApiResults.Conflict(NotTerminated);
                if (r.HiredAt <= terminated)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["hiredAt"] = [$"El reingreso es posterior a la baja ({StaffRules.Date(terminated)})."] });
                if (await Assignment(db, ctx, r.PayrollGroupId, r.PositionId, ct) is { } bad)
                    return bad;
                if (await db.Set<Employee>().AnyAsync(x => x.PersonId == previous.PersonId && x.PayrollGroupId == r.PayrollGroupId && x.TerminatedAt == null, ct))
                    return ApiResults.Conflict(AlreadyEmployed);
                var hasAccount = await StaffRules.HasAccountAsync(db, previous.PersonId, clock.Today(), ct);
                if (!hasAccount && r.CardBank is null)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["cardBank"] = [CardBankRequired] });
                var employee = new Employee
                {
                    PersonId = previous.PersonId,
                    PayrollGroupId = r.PayrollGroupId,
                    PositionId = r.PositionId,
                    Shift = Shift(r.Shift),
                    HiredAt = r.HiredAt,
                };
                db.Add(employee);
                if (!hasAccount)
                    db.Add(new CardRequest { EmployeeId = employee.Uid, Bank = r.CardBank!.Value, RequestedAt = clock.UtcNow });
                return await db.SaveOrConflictAsync(async () => Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), employee.Uid, clock.Today(), ct)),
                    AlreadyEmployed, ct);
            })
            .WithName("RehireEmployee").Mutation<RehireRequest>(CardRequestsTag).RequireAccess(Access);
    }

    // ---- Solicitar tarjeta and the «Tarjetas por solicitar» queue (SPEC §5.5, §5.7) ----

    private static void MapCards(IEndpointRouteBuilder v1, RouteGroupBuilder g)
    {
        g.MapPost("/{id:guid}/card-request", async (Guid id, CardRequestRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                if (e.TerminatedAt is not null)
                    return ApiResults.Conflict(StaffRules.AlreadyTerminated);
                if (await StaffRules.HasAccountAsync(db, e.PersonId, clock.Today(), ct))
                    return ApiResults.Conflict(HasAccountAlready);
                db.Add(new CardRequest { EmployeeId = id, Bank = r.Bank, RequestedAt = clock.UtcNow });
                return await db.SaveOrConflictAsync(async () => Detail(await EmployeeQueries.Detail(db, db.Set<Employee>(), id, clock.Today(), ct)),
                    CardAlreadyRequested, ct);
            })
            .WithName("RequestEmployeeCard").Produces<EmployeeDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, CardRequestsTag).RequireAccess(Access);

        // The Prenómina side panel reads this (S9); the alta files that consume it are S10.
        v1.MapGet("/card-requests", async (string? status, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (!WireEnum.TryParse<CardRequestStatus>(status, out var st))
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["status"] = ["Filtro desconocido."] });
                var employees = PayrollScope.Employees(db, ctx, PermissionModule.PayrollCardRequests);
                var wanted = st ?? CardRequestStatus.Queued;
                var today = clock.Today();
                return TypedResults.Ok(await db.Set<CardRequest>().AsNoTracking()
                    .Where(r => r.Status == wanted && employees.Any(e => e.Uid == r.EmployeeId))
                    // Queued but no longer needed: the person got a principal account elsewhere (Candidates app).
                    .Where(r => wanted != CardRequestStatus.Queued || !employees.Any(e => e.Uid == r.EmployeeId
                        && db.Set<PersonBankAccount>().Any(a => a.PersonId == e.PersonId && a.IsPrimary && (a.ValidTo == null || a.ValidTo >= today))))
                    .OrderBy(r => r.RequestedAt)
                    .Select(r => new CardRequestDto(r.Uid, r.EmployeeId, r.Bank, r.Status, r.RequestedAt)).ToListAsync(ct));
            })
            .WithName("ListCardRequests").WithTags("Employees").Produces<List<CardRequestDto>>().ProducesValidationProblem()
            .RequireAccess(CardRequestAccess);
    }

    // ---- Cuentas de pago (SPEC §5.8) ----

    private static void MapAccounts(RouteGroupBuilder g)
    {
        g.MapGet("/{id:guid}/bank-accounts", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                await Find(db, ctx, id, ct) is { } e
                    ? TypedResults.Ok(await BankAccounts.ListAsync(db, e.PersonId, clock.Today(), ct))
                    : ApiResults.NotFound(NotFound))
            .WithName("ListEmployeeBankAccounts").Produces<List<BankAccountDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(Access);

        g.MapPost("/{id:guid}/bank-accounts", async (Guid id, BankAccountRequest r, DbContext db, RequestContext ctx, IFieldCipher cipher,
                IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                var person = await db.Set<Person>().SingleAsync(p => p.Uid == e.PersonId, ct);
                var (account, errors) = await BankAccounts.AddAsync(db, cipher, person, r, clock.Today(), ct);
                if (errors is not null)
                    return ApiResults.Validation(errors);
                if (account!.IsPrimary)
                    await CardsNoLongerNeeded(db, e.PersonId, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await BankAccounts.ListAsync(db, e.PersonId, clock.Today(), ct));
            })
            .WithName("AddEmployeeBankAccount").Accounts<BankAccountRequest>().RequireAccess(Access);

        g.MapPut("/{id:guid}/bank-accounts/{accountId:guid}", async (Guid id, Guid accountId, UpdateBankAccountRequest r, DbContext db,
                RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                if (await db.Set<PersonBankAccount>().SingleOrDefaultAsync(a => a.Uid == accountId && a.PersonId == e.PersonId, ct) is not { } account)
                    return ApiResults.NotFound(BankAccounts.NotFound);
                if (r.ValidTo is { } to && to < account.ValidFrom)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["validTo"] = ["Es anterior al inicio de la vigencia."] });
                BankAccounts.Update(account, r);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await BankAccounts.ListAsync(db, e.PersonId, clock.Today(), ct));
            })
            .WithName("UpdateEmployeeBankAccount").Accounts<UpdateBankAccountRequest>().RequireAccess(Access);

        g.MapPost("/{id:guid}/bank-accounts/{accountId:guid}/primary", async (Guid id, Guid accountId, DbContext db, RequestContext ctx,
                IClock clock, CancellationToken ct) =>
            {
                if (await Find(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(NotFound);
                if (await db.Set<PersonBankAccount>().SingleOrDefaultAsync(a => a.Uid == accountId && a.PersonId == e.PersonId, ct) is not { } account)
                    return ApiResults.NotFound(BankAccounts.NotFound);
                if (!account.IsCurrent(clock.Today()) || account.ValidTo is not null)
                    return ApiResults.Conflict(BankAccounts.ClosedCannotBePrimary);
                await BankAccounts.SetPrimaryAsync(db, account, ct);
                await CardsNoLongerNeeded(db, e.PersonId, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await BankAccounts.ListAsync(db, e.PersonId, clock.Today(), ct));
            })
            .WithName("MakeEmployeeBankAccountPrimary").Produces<List<BankAccountDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, CardRequestsTag).RequireAccess(Access);
    }

    /// <summary>A primary account arrived: the person's queued card requests are no longer needed.</summary>
    private static Task CardsNoLongerNeeded(DbContext db, Guid personId, CancellationToken ct) =>
        StaffRules.CancelQueuedCardsAsync(db, db.Set<Employee>().Where(x => x.PersonId == personId).Select(x => x.Uid), ct);

    // ---- helpers ----

    private static RouteHandlerBuilder Mutation<TBody>(this RouteHandlerBuilder route, params string[] alsoInvalidates) where TBody : class =>
        route.Validate<TBody>().Produces<EmployeeDetailDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates([Tag, GroupEndpoints.Tag, .. alsoInvalidates]);

    private static RouteHandlerBuilder Accounts<TBody>(this RouteHandlerBuilder route) where TBody : class =>
        route.Validate<TBody>().Produces<List<BankAccountDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(Tag, CardRequestsTag);

    private static IResult Detail(EmployeeDetailDto? dto) => dto is null ? ApiResults.NotFound(NotFound) : TypedResults.Ok(dto);

    private static Task<Employee?> Find(DbContext db, RequestContext ctx, Guid id, CancellationToken ct) =>
        PayrollScope.Employees(db, ctx, PermissionModule.PayrollStaff).SingleOrDefaultAsync(e => e.Uid == id, ct);

    private static string Shift(string value) => string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>The group must be active and in the caller's scope, the puesto active and of that group.</summary>
    private static async Task<IResult?> Assignment(DbContext db, RequestContext ctx, Guid groupId, Guid positionId, CancellationToken ct)
    {
        if (!await PayrollScope.Groups(db, ctx, PermissionModule.PayrollStaff).AnyAsync(g => g.Uid == groupId && g.IsActive, ct))
            return ApiResults.Validation(new Dictionary<string, string[]> { ["payrollGroupId"] = [GroupNotFound] });
        if (!await db.Set<Position>().AnyAsync(p => p.Uid == positionId && p.PayrollGroupId == groupId && p.IsActive, ct))
            return ApiResults.Validation(new Dictionary<string, string[]> { ["positionId"] = [PositionNotInGroup] });
        return null;
    }

    private static Task<EmployeeStatusPeriod?> Overlap(DbContext db, Guid employeeId, Guid? except, DateOnly start, DateOnly? end,
        CancellationToken ct) =>
        db.Set<EmployeeStatusPeriod>().Where(p => p.EmployeeId == employeeId && p.Uid != except
                && (end == null || p.StartDate <= end) && (p.EndDate == null || p.EndDate >= start))
            .OrderBy(p => p.StartDate).FirstOrDefaultAsync(ct);

    /// <summary>Keeps <c>employees.status</c> = today's status (the S8 job rolls it every day).</summary>
    private static async Task RefreshStatus(DbContext db, Employee e, DateOnly today, CancellationToken ct, Guid? removed = null)
    {
        var periods = await db.Set<EmployeeStatusPeriod>().Where(p => p.EmployeeId == e.Uid && p.Uid != removed).ToListAsync(ct);
        periods.AddRange(db.ChangeTracker.Entries<EmployeeStatusPeriod>()
            .Where(x => x.State == EntityState.Added && x.Entity.EmployeeId == e.Uid).Select(x => x.Entity));
        e.Status = StaffRules.StatusOn(periods.DistinctBy(p => p.Uid), today);
    }

    private static async Task<List<GroupOptionDto>> GroupOptions(DbContext db, RequestContext ctx, DateOnly today, CancellationToken ct)
    {
        var groups = await PayrollScope.Groups(db, ctx, PermissionModule.PayrollStaff).Where(g => g.IsActive)
            .Join(db.Set<Client>(), g => g.ClientId, c => c.Uid, (g, c) => new { g, Client = c.ShortName })
            .OrderBy(x => x.Client).ThenBy(x => x.g.Name).AsNoTracking().ToListAsync(ct);
        var ids = groups.Select(x => x.g.Uid).ToList();
        var positions = await db.Set<Position>().Where(p => ids.Contains(p.PayrollGroupId) && p.IsActive).AsNoTracking().ToListAsync(ct);
        var positionIds = positions.Select(p => p.Uid).ToList();
        var rates = await db.Set<PositionRate>()
            .Where(r => positionIds.Contains(r.PositionId) && r.ValidFrom <= today && (r.ValidTo == null || r.ValidTo >= today))
            .AsNoTracking().ToDictionaryAsync(r => r.PositionId, ct);
        var clientIds = groups.Select(x => x.g.ClientId).Distinct().ToList();
        var sites = await db.Set<ClientSite>().Where(s => clientIds.Contains(s.ClientId) && s.IsActive).AsNoTracking().ToListAsync(ct);
        return groups.Select(x => new GroupOptionDto(x.g.Uid, x.g.ClientId, x.Client, x.g.Name,
            positions.Where(p => p.PayrollGroupId == x.g.Uid).OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new PositionOptionDto(p.Uid, p.Name, rates.TryGetValue(p.Uid, out var r) ? StaffRules.Rates(r) : null)).ToList(),
            sites.Where(s => s.ClientId == x.g.ClientId).OrderBy(s => s.Name, StringComparer.Ordinal).Select(s => new SiteOptionDto(s.Uid, s.Name)).ToList()))
            .ToList();
    }

    /// <summary>What «Convertir en empleado» pre-fills; <c>null</c> unless the candidate is Contratado.</summary>
    private static async Task<ConversionDto?> Conversion(DbContext db, Guid candidateId, DateOnly today, CancellationToken ct)
    {
        var c = await db.Set<Candidate>().AsNoTracking().SingleOrDefaultAsync(x => x.Uid == candidateId && x.Stage == CandidateStage.Hired, ct);
        if (c is null)
            return null;
        var opening = await db.Set<JobOpening>().AsNoTracking().SingleAsync(o => o.Uid == c.JobOpeningId, ct);
        var person = await db.Set<Person>().Where(p => p.Uid == c.PersonId).Select(p => p.FullName).SingleAsync(ct);
        // The bank the vacante pays with, when it issues cards (SPEC §5.7 «default from the client's BANCO preference»).
        var bank = await db.Set<Bank>().Where(b => opening.PaymentBankIds.Contains(b.Uid) && b.SupportsRegistration != null)
            .Select(b => b.SupportsRegistration).FirstOrDefaultAsync(ct);
        var name = (await OpeningQueries.DisplayNames(db, [opening.Uid], ct))[opening.Uid];
        return new ConversionDto(c.Uid, c.PersonId, person, opening.ClientId, name, c.HiredAt, bank,
            await StaffRules.HasAccountAsync(db, c.PersonId, today, ct), c.EmployeeId);
    }
}
