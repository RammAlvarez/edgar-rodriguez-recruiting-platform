using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Bank.Layouts;
using Platform.Bank.Payments;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Reports;
using Platform.Core.Security;
using Platform.Core.Users;
using Platform.Payroll;
using Platform.Payroll.Groups;
using Platform.Payroll.Staff;
using Platform.Recruiting.Reports;
using BankRow = Platform.Core.Catalogs.Bank;
using LayoutGender = Platform.Bank.Layouts.Gender;

namespace Platform.Bank.Registrations;

/// <summary>
/// Alta masiva files (SPEC §5.7, PLAN D85) — grant <c>payroll_card_requests</c>, limited to its clients: the preview of the
/// queued requests with the bank's rules, «Generar alta» (Banorte NOMINATIVA with a sucursal, or Banregio, without card
/// numbers), «Subido al banco», and «Tarjeta recibida», which makes the card the person's principal account.
/// </summary>
public static class RegistrationEndpoints
{
    public const string Tag = "card-batches";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollCardRequests);

    public const string BatchNotFound = "El lote no existe o no es de tus clientes.";
    public const string RequestNotFound = "La solicitud no existe o no es de tus clientes.";
    public const string NothingQueued = "No hay solicitudes en espera para ese banco.";
    public const string HasIssues = "El archivo tiene errores: corrige los datos de las personas marcadas.";
    public const string BranchRequired = "Escribe la sucursal Banorte (5 dígitos) a la que se envían las tarjetas.";
    public const string AlreadyUploaded = "El lote ya está marcado como subido al banco.";
    public const string NotUploaded = "Marca primero el lote como subido al banco.";
    public const string AlreadyReceived = "Esa tarjeta ya se registró.";

    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(10);

    private sealed record Candidate(CardRequest Request, Person Person, string PositionName, string ClientName, string GroupName,
        string? ExistingNumber, bool HasAccount);

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/card-batches").WithTags("CardBatches");

        g.MapGet("/preview", async (string bank, string? branchToSend, [FromQuery] Guid[]? requestIds, DbContext db, RequestContext ctx,
                IClock clock, CancellationToken ct) =>
            {
                if (!WireEnum.TryParse<RegistrationBank>(bank, out var b) || b is null)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["bank"] = ["Elige Banorte o Banregio."] });
                return TypedResults.Ok(await PreviewAsync(db, ctx, new RegistrationBatchRequest(b.Value, requestIds, branchToSend), clock.Today(), ct));
            })
            .WithName("PreviewRegistrationBatch").Produces<RegistrationPreviewDto>().ProducesValidationProblem().RequireAccess(Access);

        g.MapPost("", async (RegistrationBatchRequest r, DbContext db, RequestContext ctx, IClock clock, IObjectStorage storage,
                CancellationToken ct) =>
            {
                var today = clock.Today();
                var now = clock.UtcNow;
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var preview = await PreviewAsync(db, ctx, r, today, ct);
                if (r.Bank == RegistrationBank.Banorte && Branch(r.BranchToSend) is null)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["branchToSend"] = [BranchRequired] });
                if (!preview.CanGenerate)
                    return preview.Rows.Count == 0
                        ? ApiResults.Conflict(NothingQueued)
                        : ApiResults.Validation(new Dictionary<string, string[]>
                        {
                            ["requestIds"] = [HasIssues, .. preview.FileIssues,
                                .. preview.Rows.SelectMany(x => x.Issues.Select(i => $"{x.FullName}: {i.Field} — {i.Message}"))],
                        });

                var candidates = await CandidatesAsync(db, ctx, r, today, ct, tracked: true);
                // A request is moot once the person has a principal account, whichever app added it (D82): cancelled here.
                foreach (var moot in candidates.Where(c => c.HasAccount))
                    moot.Request.Status = CardRequestStatus.Cancelled;
                var included = candidates.Where(c => !c.HasAccount).ToList();
                var numbers = new List<string>();
                foreach (var c in included)
                {
                    if (c.ExistingNumber is { } existing)
                    {
                        numbers.Add(existing);
                        continue;
                    }
                    var next = (await (r.Bank == RegistrationBank.Banorte
                        ? db.Database.SqlQuery<long>($"SELECT nextval('platform.banorte_employee_seq') AS \"Value\"")
                        : db.Database.SqlQuery<long>($"SELECT nextval('platform.banregio_employee_seq') AS \"Value\"")).ToListAsync(ct)).Single();
                    var number = next.ToString(CultureInfo.InvariantCulture);
                    db.Add(new EmployeeBankNumber { EmployeeId = c.Request.EmployeeId, Bank = r.Bank, EmployeeNumber = number });
                    numbers.Add(number);
                }

                var settings = new BanorteBatchSettings(CardType: BanorteMassRegistration.NamedCard, BranchToSend: Branch(r.BranchToSend));
                byte[] file;
                string version;
                if (r.Bank == RegistrationBank.Banorte)
                {
                    file = Write(BanorteMassRegistration.Layout(settings), included.Select((c, i) => Banorte(c, numbers[i])).ToList(), today);
                    version = BanorteMassRegistration.Version;
                }
                else
                {
                    file = Write(BanregioRegistration.Layout(), included.Select((c, i) => Banregio(c, numbers[i])).ToList(), today);
                    version = BanregioRegistration.Version;
                }

                var batch = new RegistrationBatch
                {
                    Bank = r.Bank,
                    GeneratedBy = ctx.RequiredPrincipal.UserId,
                    GeneratedAt = now,
                    RowCount = included.Count,
                    StoragePath = "",
                    LayoutVersion = version,
                };
                if (r.Bank == RegistrationBank.Banorte)
                {
                    batch.OccupationCode = settings.OccupationCode;
                    batch.ProductCode = settings.Product;
                    batch.CardType = settings.CardType;
                    batch.GrossMonthlySalary = settings.GrossMonthlySalary;
                    batch.BranchToSend = settings.BranchToSend;
                }
                batch.StoragePath = $"bank/registrations/{now.Year}/{batch.Uid}.xlsx";
                for (var i = 0; i < included.Count; i++)
                {
                    var row = new RegistrationBatchRow
                    {
                        LineNo = i + 1,
                        CardRequestId = included[i].Request.Uid,
                        EmployeeId = included[i].Request.EmployeeId,
                        EmployeeNumber = numbers[i]
                    };
                    batch.Rows.Add(row);
                    included[i].Request.Status = CardRequestStatus.InBatch;
                    included[i].Request.RegistrationBatchRowId = row.Uid;
                }
                await storage.UploadAsync(StorageBucket.Exports, batch.StoragePath, file, ReportFormat.Xlsx.ContentType(), ct);
                db.Add(batch);
                db.Add(Export(ctx, now, new { bank = r.Bank.ToString(), batchId = batch.Uid, rows = batch.RowCount }));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return TypedResults.Ok(new GeneratedRegistrationDto((await BatchesAsync(db, ctx, batch.Uid, ct)).Single(),
                    await Link(storage, batch, ct)));
            })
            .WithName("GenerateRegistrationBatch").Produces<GeneratedRegistrationDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, EmployeeEndpoints.CardRequestsTag, EmployeeEndpoints.Tag)
            .RequireAccess(Access);

        g.MapGet("", async (DbContext db, RequestContext ctx, CancellationToken ct) => TypedResults.Ok(await BatchesAsync(db, ctx, null, ct)))
            .WithName("ListRegistrationBatches").Produces<List<RegistrationBatchDto>>().RequireAccess(Access);

        g.MapPost("/{id:guid}/download", async (Guid id, DbContext db, RequestContext ctx, IClock clock, IObjectStorage storage, CancellationToken ct) =>
            {
                if ((await BatchesAsync(db, ctx, id, ct)).Count == 0)
                    return ApiResults.NotFound(BatchNotFound);
                var batch = await db.Set<RegistrationBatch>().AsNoTracking().SingleAsync(b => b.Uid == id, ct);
                db.Add(Export(ctx, clock.UtcNow, new { download = true, batchId = id }));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await Link(storage, batch, ct));
            })
            .WithName("DownloadRegistrationBatch").Produces<ReportExportDto>().ProducesProblem(StatusCodes.Status404NotFound).Invalidates(Tag)
            .RequireAccess(Access);

        g.MapPost("/{id:guid}/uploaded", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if ((await BatchesAsync(db, ctx, id, ct)).Count == 0)
                    return ApiResults.NotFound(BatchNotFound);
                var batch = await db.Set<RegistrationBatch>().Include(b => b.Rows).SingleAsync(b => b.Uid == id, ct);
                if (batch.Status != BatchStatus.Generated)
                    return ApiResults.Conflict(AlreadyUploaded);
                batch.Status = BatchStatus.UploadedToBank;
                batch.UploadedAt = clock.UtcNow;
                batch.UploadedBy = ctx.RequiredPrincipal.UserId;
                var ids = batch.Rows.Select(x => x.CardRequestId).ToList();
                foreach (var request in await db.Set<CardRequest>().Where(x => ids.Contains(x.Uid) && x.Status == CardRequestStatus.InBatch).ToListAsync(ct))
                    request.Status = CardRequestStatus.Uploaded;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok((await BatchesAsync(db, ctx, id, ct)).Single());
            })
            .WithName("MarkRegistrationBatchUploaded").Produces<RegistrationBatchDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, EmployeeEndpoints.CardRequestsTag).RequireAccess(Access);

        // «Tarjeta recibida»: the card becomes the principal account; the next unpaid period pays into it.
        v1.MapPost("/card-requests/{id:guid}/received", async (Guid id, ReceiveCardRequest r, DbContext db, RequestContext ctx, IClock clock,
                IFieldCipher cipher, CancellationToken ct) =>
            {
                var employees = PayrollScope.Employees(db, ctx, PermissionModule.PayrollCardRequests);
                var request = await db.Set<CardRequest>().SingleOrDefaultAsync(x => x.Uid == id && employees.Any(e => e.Uid == x.EmployeeId), ct);
                if (request is null)
                    return ApiResults.NotFound(RequestNotFound);
                if (request.Status == CardRequestStatus.Received)
                    return ApiResults.Conflict(AlreadyReceived);
                if (request.Status != CardRequestStatus.Uploaded)
                    return ApiResults.Conflict(NotUploaded);
                var bank = await db.Set<BankRow>().FirstAsync(b => b.SupportsRegistration == request.Bank && b.IsActive, ct);
                var personId = await db.Set<Employee>().Where(e => e.Uid == request.EmployeeId).Select(e => e.PersonId).SingleAsync(ct);
                var person = await db.Set<Person>().SingleAsync(p => p.Uid == personId, ct);
                var today = clock.Today();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var (account, errors) = await BankAccounts.AddAsync(db, cipher, person,
                    new BankAccountRequest(bank.Uid, BankAccountType.DebitCard, r.CardNumber ?? "", null, $"Tarjeta {bank.Name}", today, MakePrimary: true),
                    today, ct);
                if (errors is not null)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["cardNumber"] = errors.Values.SelectMany(v => v).ToArray() });
                request.CardNumberEnc = account!.AccountNumberEnc;
                request.Status = CardRequestStatus.Received;
                request.ReceivedAt = clock.UtcNow;
                await db.SaveChangesAsync(ct);
                // The batch is complete once every card has arrived or was cancelled.
                if (await db.Set<RegistrationBatchRow>().Where(x => x.CardRequestId == id).Select(x => x.RegistrationBatchId).FirstOrDefaultAsync(ct)
                    is var batchId && batchId != Guid.Empty)
                {
                    var open = await db.Set<RegistrationBatchRow>().Where(x => x.RegistrationBatchId == batchId)
                        .Join(db.Set<CardRequest>(), x => x.CardRequestId, c => c.Uid, (_, c) => c.Status)
                        .AnyAsync(s => s != CardRequestStatus.Received && s != CardRequestStatus.Cancelled, ct);
                    if (!open)
                    {
                        var batch = await db.Set<RegistrationBatch>().SingleAsync(b => b.Uid == batchId, ct);
                        batch.Status = BatchStatus.Completed;
                        await db.SaveChangesAsync(ct);
                    }
                }
                await transaction.CommitAsync(ct);
                return TypedResults.Ok(BankAccounts.Dto(account, bank.Name, bank.SpeiCode, person.FullName, today));
            })
            .WithName("ReceiveCard").WithTags("CardBatches").Produces<BankAccountDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .Invalidates(Tag, EmployeeEndpoints.CardRequestsTag, EmployeeEndpoints.Tag, PaymentEndpoints.Tag, Payroll.Prepayroll.PrepayrollEndpoints.Tag)
            .RequireAccess(Access);

        return v1;
    }

    private static string? Branch(string? value) => BankText.Digits(value) is { Length: 5 } digits ? digits : null;

    private static async Task<List<Candidate>> CandidatesAsync(DbContext db, RequestContext ctx, RegistrationBatchRequest r, DateOnly today,
        CancellationToken ct, bool tracked = false)
    {
        var employees = PayrollScope.Employees(db, ctx, PermissionModule.PayrollCardRequests);
        var ids = r.RequestIds ?? [];
        var q = db.Set<CardRequest>().Where(x => x.Bank == r.Bank && x.Status == CardRequestStatus.Queued
            && employees.Any(e => e.Uid == x.EmployeeId) && (ids.Count == 0 || ids.Contains(x.Uid)));
        if (!tracked)
            q = q.AsNoTracking();
        var requests = await q.OrderBy(x => x.RequestedAt).ToListAsync(ct);
        var employeeIds = requests.Select(x => x.EmployeeId).ToList();
        var info = await db.Set<Employee>().AsNoTracking().Where(e => employeeIds.Contains(e.Uid))
            .Join(db.Set<Position>(), e => e.PositionId, p => p.Uid, (e, p) => new { e, Position = p.Name })
            .Join(db.Set<PayrollGroup>(), x => x.e.PayrollGroupId, g => g.Uid, (x, g) => new { x.e, x.Position, Group = g.Name, g.ClientId })
            .Join(db.Set<Client>(), x => x.ClientId, c => c.Uid, (x, c) => new { x.e, x.Position, x.Group, Client = c.ShortName })
            .ToDictionaryAsync(x => x.e.Uid, ct);
        var personIds = info.Values.Select(x => x.e.PersonId).ToList();
        var people = await db.Set<Person>().AsNoTracking().Where(p => personIds.Contains(p.Uid)).ToDictionaryAsync(p => p.Uid, ct);
        var withAccount = (await db.Set<PersonBankAccount>().AsNoTracking()
                .Where(a => personIds.Contains(a.PersonId) && a.IsPrimary && (a.ValidTo == null || a.ValidTo >= today)).Select(a => a.PersonId)
                .ToListAsync(ct))
            .ToHashSet();
        var numbers = await db.Set<EmployeeBankNumber>().AsNoTracking().Where(n => n.Bank == r.Bank && employeeIds.Contains(n.EmployeeId))
            .ToDictionaryAsync(n => n.EmployeeId, n => n.EmployeeNumber, ct);
        return requests.Select(x =>
            {
                var e = info[x.EmployeeId];
                return new Candidate(x, people[e.e.PersonId], e.Position, e.Client, e.Group, numbers.GetValueOrDefault(x.EmployeeId),
                    withAccount.Contains(e.e.PersonId));
            })
            .OrderBy(c => c.Person.FullName).ToList();
    }

    private static async Task<RegistrationPreviewDto> PreviewAsync(DbContext db, RequestContext ctx, RegistrationBatchRequest r, DateOnly today,
        CancellationToken ct)
    {
        var candidates = await CandidatesAsync(db, ctx, r, today, ct);
        var included = candidates.Where(c => !c.HasAccount).ToList();
        // A new person shows a placeholder number in the checks: the real one is taken from the sequence on generation.
        var prefix = r.Bank == RegistrationBank.Banorte ? 2_000_000_000L : 3_000_000_000L;
        var numbers = included.Select((c, i) => c.ExistingNumber ?? (prefix + i).ToString(CultureInfo.InvariantCulture)).ToList();
        var branch = Branch(r.BranchToSend);
        var issues = included.Count == 0
            ? []
            : r.Bank == RegistrationBank.Banorte
                ? BanorteMassRegistration.Layout(new BanorteBatchSettings(CardType: BanorteMassRegistration.NamedCard, BranchToSend: branch))
                    .Validate(included.Select((c, i) => Banorte(c, numbers[i])).ToList(), today)
                : BanregioRegistration.Layout().Validate(included.Select((c, i) => Banregio(c, numbers[i])).ToList(), today);
        // Without a sucursal yet every Banorte row would repeat it: it is asked once, above the rows.
        var fileIssues = issues.Where(i => i.Line == 0).Select(i => i.Message).ToList();
        var rows = included.Select((c, i) => new RegistrationPreviewRowDto(c.Request.Uid, c.Request.EmployeeId, c.Person.FullName, c.ClientName,
                c.GroupName, c.ExistingNumber,
                issues.Where(x => x.Line == i + 1 && !(branch is null && x.Field == "Sucursal a enviar"))
                    .Select(x => new RegistrationIssueDto(x.Field, x.Message)).ToList()))
            .ToList();
        if (r.Bank == RegistrationBank.Banorte && branch is null && included.Count > 0)
            fileIssues.Add(BranchRequired);
        return new RegistrationPreviewDto(r.Bank, r.Bank == RegistrationBank.Banorte ? BanorteMassRegistration.NamedCard : null, branch, rows,
            fileIssues, candidates.Where(c => c.HasAccount).Select(c => c.Person.FullName).ToList(),
            rows.Count > 0 && fileIssues.Count == 0 && rows.All(x => x.Issues.Count == 0));
    }

    private static BanorteRegistrationRow Banorte(Candidate c, string number) =>
        new(number, c.Person.Curp ?? "", c.Person.Email ?? "", c.Person.MobilePhone ?? "", c.Person.Street ?? "", c.Person.ExtNumber ?? "",
            c.Person.Neighborhood ?? "", c.Person.PostalCode ?? "", null, c.Person.Rfc ?? "");

    private static BanregioRegistrationRow Banregio(Candidate c, string number) =>
        new(number, c.Person.FirstNames, c.Person.LastNamePaternal, c.Person.LastNameMaternal ?? "", c.Person.Rfc ?? "", c.Person.Curp ?? "",
            c.Person.Street ?? "", c.Person.ExtNumber ?? "", c.Person.Neighborhood ?? "", c.Person.PostalCode ?? "", c.Person.MobilePhone ?? "",
            c.Person.BirthDate,
            c.Person.Gender switch { Platform.Core.People.Gender.Male => LayoutGender.Male, Platform.Core.People.Gender.Female => LayoutGender.Female, _ => null },
            c.PositionName, c.Person.Email ?? "", null, c.Person.BirthState ?? "");

    private static async Task<List<RegistrationBatchDto>> BatchesAsync(DbContext db, RequestContext ctx, Guid? id, CancellationToken ct)
    {
        var employees = PayrollScope.Employees(db, ctx, PermissionModule.PayrollCardRequests);
        var q = db.Set<RegistrationBatch>().AsNoTracking().Include(b => b.Rows)
            // A batch is visible when one of its people is in scope; a client-limited grant sees only its people's rows.
            .Where(b => b.Rows.Any(x => employees.Any(e => e.Uid == x.EmployeeId)));
        if (id is { } one)
            q = q.Where(b => b.Uid == one);
        var batches = await q.OrderByDescending(b => b.GeneratedAt).Take(50).ToListAsync(ct);
        var requestIds = batches.SelectMany(b => b.Rows).Select(x => x.CardRequestId).ToList();
        var inScope = await employees.Select(e => e.Uid).ToListAsync(ct);
        var statuses = await db.Set<CardRequest>().Where(c => requestIds.Contains(c.Uid)).ToDictionaryAsync(c => c.Uid, c => c.Status, ct);
        var employeeIds = batches.SelectMany(b => b.Rows).Select(x => x.EmployeeId).Distinct().ToList();
        var names = await db.Set<Employee>().Where(e => employeeIds.Contains(e.Uid))
            .Join(db.Set<Person>(), e => e.PersonId, p => p.Uid, (e, p) => new { e.Uid, p.FullName }).ToDictionaryAsync(x => x.Uid, x => x.FullName, ct);
        var userIds = batches.Select(b => b.GeneratedBy).Distinct().ToList();
        var users = await db.Set<User>().Where(u => userIds.Contains(u.Uid)).ToDictionaryAsync(u => u.Uid, u => u.DisplayName, ct);
        return batches.Select(b => new RegistrationBatchDto(b.Uid, b.Bank, b.Status, b.GeneratedAt, users[b.GeneratedBy], b.UploadedAt, b.CardType,
                b.BranchToSend,
                b.Rows.Where(x => inScope.Contains(x.EmployeeId)).OrderBy(x => x.LineNo)
                    .Select(x => new RegistrationBatchRowDto(x.CardRequestId, x.EmployeeId, names[x.EmployeeId], x.EmployeeNumber, statuses[x.CardRequestId]))
                    .ToList()))
            .ToList();
    }

    private static byte[] Write<TRow>(BankLayout<TRow> layout, IReadOnlyList<TRow> rows, DateOnly today)
    {
        using var stream = new MemoryStream();
        BankFileWriter.Write(layout, rows, today, stream);
        return stream.ToArray();
    }

    private static async Task<ReportExportDto> Link(IObjectStorage storage, RegistrationBatch batch, CancellationToken ct)
    {
        // The date as the owner's day in Monterrey, not UTC's.
        var day = TimeZoneInfo.ConvertTime(batch.GeneratedAt, TimeZoneInfo.FindSystemTimeZoneById(ClockExtensions.DefaultTimeZone))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var name = batch.Bank == RegistrationBank.Banorte ? $"ALTA MASIVA BANORTE {day}.xlsx" : $"ALTA BANREGIO {day}.xlsx";
        var download = await storage.PresignDownloadAsync(StorageBucket.Exports, batch.StoragePath, name, DownloadLifetime, ct);
        return new ReportExportDto(download.Url, name, download.ExpiresAt);
    }

    private static AuditEntry Export(RequestContext ctx, DateTimeOffset now, object after) => new()
    {
        OccurredAt = now,
        UserId = ctx.RequiredPrincipal.UserId,
        ActingForUserId = ctx.ActingFor?.UserId,
        Action = AuditAction.Export,
        SchemaName = "platform",
        TableName = "registration_batches",
        After = JsonSerializer.Serialize(after),
        RequestId = ctx.RequestId,
    };
}
