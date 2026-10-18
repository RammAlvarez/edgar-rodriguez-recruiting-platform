using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Bank.Layouts;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Catalogs;
using Platform.Core.Http;
using Platform.Core.Reports;
using Platform.Core.Security;
using Platform.Core.Users;
using Platform.Payroll;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;
using Platform.Recruiting.Reports;

namespace Platform.Bank.Payments;

/// <summary>
/// Archivos de pago (SPEC §5.5, PLAN D86) — grant <c>payroll_payment_files</c>, limited to its clients: the payment panel of a
/// period, «Generar archivos de pago» (the period's file; later, the pending lines' file), downloads, «Subido al banco»,
/// rechazado, the deposit date, and the cuenta de cargo.
/// </summary>
public static class PaymentEndpoints
{
    public const string Tag = "payments";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollPaymentFiles);

    public const string PeriodNotFound = "El período no existe o no es de tus clientes.";
    public const string BatchNotFound = "El archivo no existe o no es de tus clientes.";
    public const string RowNotFound = "El renglón no existe o no es de tus clientes.";
    public const string NotPaid = "El período aún no se paga.";
    public const string AlreadyUploaded = "El archivo ya está marcado como subido al banco.";
    public const string NotUploaded = "Marca primero el archivo como subido al banco.";
    public const string LineNotFound = "La línea no existe o no es de tus clientes.";
    public const string NotOwed = "Esta línea ya no está pendiente de pago.";
    public const string NoManualPayment = "Esta línea no tiene un pago fuera de archivo.";
    public const string RowNotPaid = "Solo se rechaza un pago que el banco recibió.";
    public const string NoFrequentFile = "Este archivo no llevó cuentas frecuentes.";
    public const string PayingAccountNeedsAllClients = "Solo un administrador con Archivos de pago para todos los clientes cambia la cuenta de cargo.";

    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(10);

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/payroll").WithTags("Payments");

        g.MapGet("/periods/{id:guid}/payments", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                await PanelAsync(db, ctx, id, clock.Today(), ct) is { } panel ? TypedResults.Ok(panel) : ApiResults.NotFound(PeriodNotFound))
            .WithName("GetPeriodPayments").Produces<PeriodPaymentsDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Access);

        // In Revisión: the period's file, and the period becomes Pagada. Once paid: a file of the lines still pending.
        g.MapPost("/periods/{id:guid}/payment-files", async (Guid id, DbContext db, RequestContext ctx, IClock clock, IFieldCipher cipher,
                IObjectStorage storage, CancellationToken ct) =>
            {
                if (await Period(db, ctx, id, ct) is null)
                    return ApiResults.NotFound(PeriodNotFound);
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                // Two clicks never make two files: the second waits here and then finds the period paid.
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM platform.payroll_periods WHERE uid = {id} FOR UPDATE", ct);
                var period = await db.Set<PayrollPeriod>().SingleAsync(p => p.Uid == id, ct);
                if (period.Status == PeriodStatus.Capturing)
                    return ApiResults.Conflict(PaymentFiles.NotReviewed);
                var paying = await db.Set<PayingAccount>().AsNoTracking().OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
                if (paying is null)
                    return ApiResults.Conflict(PaymentFiles.NoPayingAccount);

                var today = clock.Today();
                var now = clock.UtcNow;
                var first = period.Status == PeriodStatus.InReview;
                var lines = await PaymentFiles.LoadAsync(db, id, today, ct);
                var candidates = PaymentFiles.NextFile(lines, period.Status).ToList();
                if (candidates.Count == 0)
                    return ApiResults.Conflict(first ? PaymentFiles.NothingToPay : PaymentFiles.NothingPending);

                var knownCodes = (await db.Set<Platform.Core.Catalogs.Bank>().Where(b => b.IsActive).Select(b => b.SpeiCode).ToListAsync(ct))
                    .ToHashSet();
                var payer = new AztecaPayingAccount(cipher.Decrypt(paying.ChargeAccountNumberEnc), paying.HolderName, paying.HolderRfc,
                    paying.DefaultChargeLegend, paying.DefaultCreditLegend, paying.Currency);
                var layout = AztecaMassPayment.Layout(payer, knownCodes);
                var userId = ctx.RequiredPrincipal.UserId;

                // Every number read in clear is logged (data_access_log, decrypt_account).
                var drafts = candidates.Select(x =>
                    {
                        db.Add(new DataAccessEntry
                        {
                            OccurredAt = now,
                            UserId = userId,
                            ActingForUserId = ctx.ActingFor?.UserId,
                            PersonId = x.PersonId,
                            Access = DataAccessKind.DecryptAccount,
                            RequestId = ctx.RequestId,
                        });
                        var digits = cipher.Decrypt(x.Account!.AccountNumberEnc);
                        return (Line: x, Digits: digits, Row: new AztecaPaymentRow(x.AccountBank!.SpeiCode, PaymentFiles.LayoutType(x.Account.AccountType),
                            digits, x.Account.HolderName, x.Entity.TotalNet));
                    })
                    .ToList();
                // A row the bank would refuse stays out (cuenta inválida) instead of blocking everyone else's pay.
                var refused = layout.Validate(drafts.Select(d => d.Row).ToList(), today).Where(i => i.Line > 0).Select(i => i.Line - 1).ToHashSet();
                var skipped = drafts.Where((_, i) => refused.Contains(i))
                    .Select(d => $"{d.Line.FullName}: {layout.Validate([d.Row], today)[0].Message}").ToList();
                foreach (var d in drafts.Where((_, i) => refused.Contains(i)))
                    d.Line.Entity.PaymentStatus = PaymentRowStatus.InvalidAccount;
                var paid = drafts.Where((_, i) => !refused.Contains(i)).ToList();
                if (paid.Count == 0)
                    return ApiResults.Conflict(first ? PaymentFiles.NothingToPay : PaymentFiles.NothingPending);

                var batch = new PaymentBatch
                {
                    PayingAccountId = paying.Uid,
                    Kind = first ? PaymentBatchKind.PeriodPayment : PaymentBatchKind.PendingPayment,
                    PayrollPeriodId = id,
                    GeneratedBy = userId,
                    GeneratedAt = now,
                    RowCount = paid.Count,
                    TotalAmount = paid.Sum(d => d.Row.Amount),
                    StoragePath = "",
                    LayoutVersion = AztecaMassPayment.Version,
                };
                var frequent = new List<FrequentAccountRow>();
                for (var i = 0; i < paid.Count; i++)
                {
                    var (x, digits, row) = paid[i];
                    var registers = !x.Account!.IsFrequentAccountRegistered;
                    if (registers)
                        frequent.Add(new FrequentAccountRow(x.ClientTag, x.Account.HolderName, row.AccountType, digits, row.ReceivingBankCode));
                    batch.Rows.Add(new PaymentBatchRow
                    {
                        LineNo = i + 1,
                        PrepayrollLineId = x.Entity.Uid,
                        PersonBankAccountId = x.Account.Uid,
                        DisplayName = x.Account.HolderName,
                        ReceivingBankCode = row.ReceivingBankCode,
                        AccountType = x.Account.AccountType,
                        AccountNumberEnc = x.Account.AccountNumberEnc,
                        AccountLast4 = x.Account.AccountLast4,
                        PaymentType = PaymentFiles.PaymentTypeFor(row.ReceivingBankCode),
                        Amount = row.Amount,
                        ChargeLegend = payer.ChargeLegend,
                        CreditLegend = payer.CreditLegend,
                        RegistersFrequentAccount = registers,
                    });
                    x.Entity.BankId = x.Account.BankId;
                    x.Entity.AccountType = x.Account.AccountType;
                    x.Entity.AccountNumberEnc = x.Account.AccountNumberEnc;
                    x.Entity.AccountLast4 = x.Account.AccountLast4;
                    x.Entity.HolderName = x.Account.HolderName;
                    x.Entity.PaymentStatus = PaymentRowStatus.Pending;
                }
                if (first)
                    foreach (var x in lines.Where(x => x.Entity.PaymentStatus is null && x.Entity.TotalNet > 0))
                        x.Entity.PaymentStatus = PaymentRowStatus.MissingAccount;

                var heading = await HeadingAsync(db, period, ct);
                var key = $"bank/payments/{period.Year}/{id}/{batch.Uid}";
                batch.StoragePath = $"{key}.xlsx";
                await storage.UploadAsync(StorageBucket.Exports, batch.StoragePath, Write(layout, paid.Select(d => d.Row).ToList(), today),
                    ReportFormat.Xlsx.ContentType(), ct);
                if (frequent.Count > 0)
                {
                    batch.FrequentAccountsStoragePath = $"{key}-frecuentes.xlsx";
                    batch.FrequentAccountsCount = frequent.Count;
                    await storage.UploadAsync(StorageBucket.Exports, batch.FrequentAccountsStoragePath,
                        Write(AztecaFrequentAccounts.Layout(knownCodes), frequent, today), ReportFormat.Xlsx.ContentType(), ct);
                }
                db.Add(batch);
                // Lines first: the period turns Pagada after them.
                await db.SaveChangesAsync(ct);
                if (first)
                {
                    period.Status = PeriodStatus.Paid;
                    period.PaidAt = now;
                    period.PaidBy = userId;
                    period.TotalNet = lines.Sum(x => x.Entity.TotalNet);
                }
                db.Add(Export(ctx, now, "payment_batches", new
                {
                    kind = batch.Kind.ToString(),
                    periodId = id,
                    batchId = batch.Uid,
                    rows = batch.RowCount,
                    total = batch.TotalAmount,
                    frequent = batch.FrequentAccountsCount
                }));
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                var dto = (await PaymentFiles.BatchesAsync(db, id, ct)).Single(b => b.Id == batch.Uid);
                return TypedResults.Ok(new GeneratedPaymentFilesDto(dto,
                    await Link(storage, batch.StoragePath, $"PAGOS {heading}.xlsx", ct),
                    batch.FrequentAccountsStoragePath is { } f ? await Link(storage, f, $"CUENTAS FRECUENTES {heading}.xlsx", ct) : null,
                    skipped));
            })
            .WithName("GeneratePaymentFiles").Produces<GeneratedPaymentFilesDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag, IncidentEndpoints.Tag).RequireAccess(Access);

        g.MapPost("/payment-batches/{id:guid}/download", async (Guid id, PaymentDownloadRequest r, DbContext db, RequestContext ctx, IClock clock,
                IObjectStorage storage, CancellationToken ct) =>
            {
                var batch = await Batch(db, ctx, id, ct);
                if (batch is null)
                    return ApiResults.NotFound(BatchNotFound);
                var file = r.File;
                var path = file == PaymentFile.Payments ? batch.StoragePath : batch.FrequentAccountsStoragePath;
                if (path is null)
                    return ApiResults.Conflict(NoFrequentFile);
                var period = await db.Set<PayrollPeriod>().AsNoTracking().SingleAsync(p => p.Uid == batch.PayrollPeriodId, ct);
                var heading = await HeadingAsync(db, period, ct);
                db.Add(Export(ctx, clock.UtcNow, "payment_batches", new { file = file.ToString(), batchId = id }));
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await Link(storage, path, $"{(file == PaymentFile.Payments ? "PAGOS" : "CUENTAS FRECUENTES")} {heading}.xlsx", ct));
            })
            .WithName("DownloadPaymentFile").Produces<ReportExportDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag).RequireAccess(Access);

        // «Subido al banco»: its rows are paid and its new accounts registered as cuentas frecuentes.
        g.MapPost("/payment-batches/{id:guid}/uploaded", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var batch = await Batch(db, ctx, id, ct, tracked: true);
                if (batch is null)
                    return ApiResults.NotFound(BatchNotFound);
                if (batch.Status != BatchStatus.Generated)
                    return ApiResults.Conflict(AlreadyUploaded);
                batch.Status = BatchStatus.UploadedToBank;
                batch.UploadedAt = clock.UtcNow;
                batch.UploadedBy = ctx.RequiredPrincipal.UserId;
                var lineIds = batch.Rows.Select(r => r.PrepayrollLineId).ToList();
                var lines = await db.Set<PrepayrollLine>().Where(l => lineIds.Contains(l.Uid)).ToDictionaryAsync(l => l.Uid, ct);
                var registered = batch.Rows.Where(r => r.RegistersFrequentAccount).Select(r => r.PersonBankAccountId).ToList();
                foreach (var account in await db.Set<Platform.Core.People.PersonBankAccount>().Where(a => registered.Contains(a.Uid)).ToListAsync(ct))
                    account.IsFrequentAccountRegistered = true;
                foreach (var row in batch.Rows.Where(r => r.Status == PaymentRowStatus.Pending))
                {
                    row.Status = PaymentRowStatus.Paid;
                    if (lines[row.PrepayrollLineId].PaymentStatus == PaymentRowStatus.Pending)
                        lines[row.PrepayrollLineId].PaymentStatus = PaymentRowStatus.Paid;
                }
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok((await PaymentFiles.BatchesAsync(db, batch.PayrollPeriodId!.Value, ct)).Single(b => b.Id == id));
            })
            .WithName("MarkPaymentBatchUploaded").Produces<PaymentBatchDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag).RequireAccess(Access);

        // «Rechazado por el banco»: the line goes back to pending, for the next pending-payments file.
        g.MapPost("/payment-batch-rows/{id:guid}/reject", async (Guid id, RejectPaymentRowRequest r, DbContext db, RequestContext ctx,
                CancellationToken ct) =>
            {
                var row = await db.Set<PaymentBatchRow>().SingleOrDefaultAsync(x => x.Uid == id, ct);
                if (row is null || await Batch(db, ctx, row.PaymentBatchId, ct) is not { } batch)
                    return ApiResults.NotFound(RowNotFound);
                if (batch.Status == BatchStatus.Generated)
                    return ApiResults.Conflict(NotUploaded);
                if (row.Status != PaymentRowStatus.Paid)
                    return ApiResults.Conflict(RowNotPaid);
                if (r.Reason.Clean() is not { } reason || reason.Length > 200)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["reason"] = ["Escribe el motivo del rechazo (hasta 200 caracteres)."] });
                row.Status = PaymentRowStatus.Rejected;
                row.RejectionReason = reason;
                var line = await db.Set<PrepayrollLine>().SingleAsync(l => l.Uid == row.PrepayrollLineId, ct);
                line.PaymentStatus = PaymentRowStatus.Rejected;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok((await PaymentFiles.BatchesAsync(db, batch.PayrollPeriodId!.Value, ct)).Single(b => b.Id == batch.Uid));
            })
            .WithName("RejectPaymentRow").Produces<PaymentBatchDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag).RequireAccess(Access);

        // «Pagado fuera de archivo»: cash, cheque or another transfer for a line no file can pay (no account, an invalid one, or
        // rejected by the bank). Only once the period is paid; the line becomes Pagado and the payment is recorded.
        g.MapPost("/payment-lines/{id:guid}/manual-payment", async (Guid id, ManualPaymentRequest r, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                var line = await db.Set<PrepayrollLine>().SingleOrDefaultAsync(l => l.Uid == id, ct);
                if (line is null || await Period(db, ctx, line.PayrollPeriodId, ct) is not { } period)
                    return ApiResults.NotFound(LineNotFound);
                if (period.Status != PeriodStatus.Paid)
                    return ApiResults.Conflict(NotPaid);
                var errors = new Dictionary<string, string[]>();
                if (!Enum.IsDefined(r.Method))
                    errors["method"] = ["Elige cómo se pagó."];
                var reference = r.Reference.Clean();
                if (reference is { Length: > 100 })
                    errors["reference"] = ["La referencia admite hasta 100 caracteres."];
                if (r.PaidOn < period.StartDate)
                    errors["paidOn"] = ["El pago no puede ser antes del período."];
                else if (r.PaidOn > clock.Today())
                    errors["paidOn"] = ["La fecha de pago no puede ser futura."];
                if (errors.Count > 0)
                    return ApiResults.Validation(errors);
                if (await db.Set<ManualPayment>().AnyAsync(m => m.PrepayrollLineId == id, ct)
                    || line.TotalNet <= 0 || line.PaymentStatus is not { } status || !PaymentFiles.Unpaid.Contains(status))
                    return ApiResults.Conflict(NotOwed);
                db.Add(new ManualPayment
                {
                    PrepayrollLineId = id,
                    Method = r.Method,
                    Reference = reference,
                    PaidOn = r.PaidOn,
                    Amount = line.TotalNet,
                    PreviousStatus = status,
                });
                line.PaymentStatus = PaymentRowStatus.Paid;
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("RecordManualPayment").Produces(StatusCodes.Status204NoContent).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .Invalidates(Tag, PrepayrollEndpoints.Tag).RequireAccess(Access);

        // A wrong entry: the line goes back to what it was and is owed again.
        g.MapDelete("/payment-lines/{id:guid}/manual-payment", async (Guid id, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var line = await db.Set<PrepayrollLine>().SingleOrDefaultAsync(l => l.Uid == id, ct);
                if (line is null || await Period(db, ctx, line.PayrollPeriodId, ct) is null)
                    return ApiResults.NotFound(LineNotFound);
                if (await db.Set<ManualPayment>().SingleOrDefaultAsync(m => m.PrepayrollLineId == id, ct) is not { } manual)
                    return ApiResults.Conflict(NoManualPayment);
                line.PaymentStatus = manual.PreviousStatus;
                db.Remove(manual);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("UndoManualPayment").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag).RequireAccess(Access);

        g.MapPut("/periods/{id:guid}/deposit-date", async (Guid id, DepositDateRequest r, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                if (await Period(db, ctx, id, ct) is null)
                    return ApiResults.NotFound(PeriodNotFound);
                var period = await db.Set<PayrollPeriod>().SingleAsync(p => p.Uid == id, ct);
                if (period.Status != PeriodStatus.Paid)
                    return ApiResults.Conflict(NotPaid);
                if (r.Date is { } d && d < period.StartDate)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["date"] = ["El depósito no puede ser antes del período."] });
                period.DepositDate = r.Date;
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("SetPeriodDepositDate").Produces(StatusCodes.Status204NoContent).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, PrepayrollEndpoints.Tag)
            .RequireAccess(Access);

        // 204 until the owner types the cuenta de cargo.
        g.MapGet("/paying-account", async (DbContext db, CancellationToken ct) =>
                await db.Set<PayingAccount>().AsNoTracking().OrderBy(a => a.Id).FirstOrDefaultAsync(ct) is { } a
                    ? Results.Ok(PaymentFiles.Dto(a))
                    : Results.NoContent())
            .WithName("GetPayingAccount").Produces<PayingAccountDto>().Produces(StatusCodes.Status204NoContent).RequireAccess(Access);

        // One account for every client: only a grant over all of them changes it.
        g.MapPut("/paying-account", async (PayingAccountRequest r, DbContext db, RequestContext ctx, IFieldCipher cipher, CancellationToken ct) =>
            {
                if (ctx.RequiredPrincipal.ClientScope(PermissionModule.PayrollPaymentFiles) is not null)
                    return ApiResults.Forbidden(PayingAccountNeedsAllClients);
                var account = await db.Set<PayingAccount>().OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
                var errors = new Dictionary<string, string[]>();
                var digits = BankText.Digits(r.ChargeAccountNumber);
                if (digits.Length > 0 && digits.Length is not (14 or 16 or 18 or 20))
                    errors["chargeAccountNumber"] = ["La cuenta de cargo tiene 14, 16, 18 o 20 dígitos."];
                if (digits.Length == 0 && account is null)
                    errors["chargeAccountNumber"] = ["Escribe la cuenta de cargo."];
                if (r.Name.Clean() is not { Length: <= 80 })
                    errors["name"] = ["Escribe un nombre (hasta 80 caracteres)."];
                if (r.HolderName.Clean() is not { Length: <= 40 })
                    errors["holderName"] = ["Escribe el titular (hasta 40 caracteres)."];
                var rfc = BankText.Normalize(r.HolderRfc ?? "");
                if (!Platform.Core.People.MexicanIds.IsCompanyRfc(rfc) && !Platform.Core.People.MexicanIds.IsPersonRfc(rfc))
                    errors["holderRfc"] = ["RFC inválido."];
                if (r.ChargeLegend.Clean() is { Length: > 30 })
                    errors["chargeLegend"] = ["La leyenda admite hasta 30 caracteres."];
                if (r.CreditLegend.Clean() is { Length: > 30 })
                    errors["creditLegend"] = ["La leyenda admite hasta 30 caracteres."];
                if (errors.Count > 0)
                    return ApiResults.Validation(errors);
                var azteca = await db.Set<Platform.Core.Catalogs.Bank>().SingleAsync(b => b.SpeiCode == AztecaMassPayment.AztecaBankCode, ct);
                if (account is null)
                    db.Add(account = new PayingAccount
                    {
                        Name = "",
                        BankId = azteca.Uid,
                        ChargeAccountNumberEnc = cipher.Encrypt(digits),
                        ChargeAccountLast4 = digits[^4..],
                        HolderName = "",
                        HolderRfc = "",
                    });
                else if (digits.Length > 0)
                {
                    account.ChargeAccountNumberEnc = cipher.Encrypt(digits);
                    account.ChargeAccountLast4 = digits[^4..];
                }
                account.Name = r.Name.Clean()!;
                account.HolderName = r.HolderName.Clean()!.ToUpperInvariant();
                account.HolderRfc = BankText.Normalize(r.HolderRfc!);
                account.DefaultChargeLegend = r.ChargeLegend.Clean() ?? "PAGO";
                account.DefaultCreditLegend = r.CreditLegend.Clean() ?? "PAGO";
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(PaymentFiles.Dto(account));
            })
            .WithName("SetPayingAccount").Produces<PayingAccountDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status403Forbidden)
            .Invalidates(Tag).RequireAccess(Access);

        return v1;
    }

    public static IQueryable<PayrollGroup> Groups(DbContext db, RequestContext ctx) =>
        PayrollScope.Groups(db, ctx, PermissionModule.PayrollPaymentFiles);

    private static async Task<PayrollPeriod?> Period(DbContext db, RequestContext ctx, Guid id, CancellationToken ct)
    {
        var groups = Groups(db, ctx);
        return await db.Set<PayrollPeriod>().AsNoTracking().SingleOrDefaultAsync(p => p.Uid == id && groups.Any(g => g.Uid == p.PayrollGroupId), ct);
    }

    private static async Task<PaymentBatch?> Batch(DbContext db, RequestContext ctx, Guid id, CancellationToken ct, bool tracked = false)
    {
        var q = db.Set<PaymentBatch>().Include(b => b.Rows).AsQueryable();
        if (!tracked)
            q = q.AsNoTracking();
        var batch = await q.SingleOrDefaultAsync(b => b.Uid == id, ct);
        return batch?.PayrollPeriodId is { } p && await Period(db, ctx, p, ct) is not null ? batch : null;
    }

    private static async Task<PeriodPaymentsDto?> PanelAsync(DbContext db, RequestContext ctx, Guid id, DateOnly today, CancellationToken ct)
    {
        var period = await Period(db, ctx, id, ct);
        if (period is null)
            return null;
        var lines = await PaymentFiles.LoadAsync(db, id, today, ct);
        var next = PaymentFiles.NextFile(lines, period.Status).Select(x => x.Entity.Uid).ToHashSet();
        var paying = await db.Set<PayingAccount>().AsNoTracking().OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
        var dtos = lines.Select(x =>
            {
                var paid = period.Status == PeriodStatus.Paid && x.Entity.PaymentStatus is PaymentRowStatus.Pending or PaymentRowStatus.Paid;
                var (bank, last4, type, holder) = paid
                    ? (x.SnapshotBank?.Name, x.Entity.AccountLast4, x.Entity.AccountType, x.Entity.HolderName)
                    : (x.AccountBank?.Name, x.Account?.AccountLast4, x.Account?.AccountType, x.Account?.HolderName);
                return new PaymentLineDto(x.Entity.Uid, x.Entity.EmployeeId, x.FullName, x.Entity.TotalNet, PaymentFiles.State(x, period.Status), bank,
                    last4, type, holder is not null && !Platform.Core.People.BankAccounts.SameName(holder, x.FullName) ? holder : null,
                    next.Contains(x.Entity.Uid) && !x.Account!.IsFrequentAccountRegistered, next.Contains(x.Entity.Uid),
                    PaymentFiles.CanPayManually(x, period.Status), x.Manual is { } m ? new ManualPaymentDto(m.Method, m.Reference, m.PaidOn) : null);
            })
            .ToList();
        var totals = dtos.GroupBy(d => d.State).OrderBy(g => g.Key)
            .Select(g => new PaymentStateTotalDto(g.Key, g.Count(), g.Sum(d => d.Amount))).ToList();
        var cannot = period.Status == PeriodStatus.Capturing ? PaymentFiles.NotReviewed
            : paying is null ? PaymentFiles.NoPayingAccount
            : next.Count == 0 ? (period.Status == PeriodStatus.InReview ? PaymentFiles.NothingToPay : PaymentFiles.NothingPending)
            : null;
        var payable = dtos.Where(d => d.InNextFile).ToList();
        return new PeriodPaymentsDto(id, period.Status, period.TotalNet ?? lines.Sum(x => x.Entity.TotalNet), period.PaidAt, period.DepositDate,
            paying is null ? null : PaymentFiles.Dto(paying), dtos, totals, payable.Count, payable.Sum(d => d.Amount),
            payable.Count(d => d.NeedsFrequentAccount), cannot is null, cannot, await PaymentFiles.BatchesAsync(db, id, ct));
    }

    /// <summary>«SEM 38 ECOVIA VALIDADORES 12 H»: the period, client and group in a file name.</summary>
    private static async Task<string> HeadingAsync(DbContext db, PayrollPeriod period, CancellationToken ct)
    {
        var x = await db.Set<PayrollGroup>().Where(g => g.Uid == period.PayrollGroupId)
            .Join(db.Set<Platform.Core.Clients.Client>(), g => g.ClientId, c => c.Uid, (g, c) => new { g.Name, g.PeriodType, Client = c.ShortName })
            .SingleAsync(ct);
        return $"{(x.PeriodType == PeriodType.Weekly ? "SEM" : "QNA")} {period.PeriodNumber} {x.Client} {x.Name}";
    }

    private static byte[] Write<TRow>(BankLayout<TRow> layout, IReadOnlyList<TRow> rows, DateOnly today)
    {
        using var stream = new MemoryStream();
        BankFileWriter.Write(layout, rows, today, stream);
        return stream.ToArray();
    }

    private static async Task<ReportExportDto> Link(IObjectStorage storage, string key, string fileName, CancellationToken ct)
    {
        var download = await storage.PresignDownloadAsync(StorageBucket.Exports, key, fileName, DownloadLifetime, ct);
        return new ReportExportDto(download.Url, fileName, download.ExpiresAt);
    }

    private static AuditEntry Export(RequestContext ctx, DateTimeOffset now, string table, object after) => new()
    {
        OccurredAt = now,
        UserId = ctx.RequiredPrincipal.UserId,
        ActingForUserId = ctx.ActingFor?.UserId,
        Action = AuditAction.Export,
        SchemaName = "platform",
        TableName = table,
        After = JsonSerializer.Serialize(after),
        RequestId = ctx.RequestId,
    };
}
