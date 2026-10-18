using Microsoft.EntityFrameworkCore;
using Platform.Bank.Layouts;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Prepayroll;
using Platform.Payroll.Staff;

using BankRow = Platform.Core.Catalogs.Bank;

namespace Platform.Bank.Payments;

/// <summary>
/// «Generar archivos de pago» (SPEC §5.5, PLAN D86): which lines can be paid, and the Banco Azteca file (with its cuentas
/// frecuentes companion) that pays them. The period's first file moves it to Pagada; later files pay the lines left pending.
/// </summary>
public static class PaymentFiles
{
    public const string NotReviewed = "Cierra la captura primero: el período se paga desde Revisión.";
    public const string NoPayingAccount = "Falta la cuenta de cargo (Banco Azteca) de la que salen los pagos.";
    public const string NothingToPay = "Ninguna línea tiene una cuenta lista para pagar.";
    public const string NothingPending = "No hay líneas pendientes con una cuenta lista.";

    /// <summary>A line as the payment panel needs it: the person, the client, and the live principal account.</summary>
    public sealed record Line(PrepayrollLine Entity, string FullName, Guid PersonId, string ClientTag, PersonBankAccount? Account,
        BankRow? AccountBank, BankRow? SnapshotBank, bool CardRequested, ManualPayment? Manual);

    /// <summary>Lines whose stored state lets a later file pay them.</summary>
    public static readonly PaymentRowStatus[] Unpaid = [PaymentRowStatus.MissingAccount, PaymentRowStatus.InvalidAccount, PaymentRowStatus.Rejected];

    public static async Task<List<Line>> LoadAsync(DbContext db, Guid periodId, DateOnly today, CancellationToken ct)
    {
        var rows = await db.Set<PrepayrollLine>().Where(l => l.PayrollPeriodId == periodId)
            .Join(db.Set<Employee>(), l => l.EmployeeId, e => e.Uid, (l, e) => new { l, e.PersonId })
            .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.l, x.PersonId, p.FullName })
            .OrderBy(x => x.FullName).ToListAsync(ct);
        var people = rows.Select(r => r.PersonId).Distinct().ToList();
        var employees = rows.Select(r => r.l.EmployeeId).ToList();
        var accounts = (await db.Set<PersonBankAccount>()
                .Where(a => people.Contains(a.PersonId) && a.IsPrimary && (a.ValidTo == null || a.ValidTo >= today)).ToListAsync(ct))
            .GroupBy(a => a.PersonId).ToDictionary(g => g.Key, g => g.First());
        var banks = await db.Set<BankRow>().AsNoTracking().ToDictionaryAsync(b => b.Uid, ct);
        var cards = (await db.Set<CardRequest>().AsNoTracking()
                .Where(r => employees.Contains(r.EmployeeId)
                    && (r.Status == CardRequestStatus.Queued || r.Status == CardRequestStatus.InBatch || r.Status == CardRequestStatus.Uploaded))
                .Select(r => r.EmployeeId).ToListAsync(ct))
            .ToHashSet();
        var lineIds = rows.Select(r => r.l.Uid).ToList();
        var manual = await db.Set<ManualPayment>().AsNoTracking().Where(m => lineIds.Contains(m.PrepayrollLineId)).ToDictionaryAsync(m => m.PrepayrollLineId, ct);
        var tag = await db.Set<PayrollPeriod>().Where(p => p.Uid == periodId)
            .Join(db.Set<PayrollGroup>(), p => p.PayrollGroupId, g => g.Uid, (p, g) => g.ClientId)
            .Join(db.Set<Client>(), id => id, c => c.Uid, (_, c) => c.ShortName).SingleAsync(ct);
        return rows.Select(r =>
            {
                var account = accounts.GetValueOrDefault(r.PersonId);
                return new Line(r.l, r.FullName, r.PersonId, ClientTag(tag), account, account is null ? null : banks[account.BankId],
                    r.l.BankId is { } b ? banks[b] : null, cards.Contains(r.l.EmployeeId), manual.GetValueOrDefault(r.l.Uid));
            })
            .ToList();
    }

    /// <summary>«hfc»: the client's short name as the cuentas frecuentes file tags it (≤ 20).</summary>
    public static string ClientTag(string shortName)
    {
        var tag = shortName.Trim().ToLowerInvariant();
        return tag.Length <= 20 ? tag : tag[..20];
    }

    /// <summary>What the line's live account allows today: an amount and a principal account make it payable.</summary>
    public static LinePaymentState LiveState(Line x) =>
        x.Entity.TotalNet <= 0 ? LinePaymentState.NothingDue
        : x.Account is null ? (x.CardRequested ? LinePaymentState.CardRequested : LinePaymentState.MissingAccount)
        : LinePaymentState.Ready;

    /// <summary>
    /// Before payment the live state; after, the one its files left it in — except a line left without an account that has
    /// one now, which is Lista for the pending-payments file.
    /// </summary>
    public static LinePaymentState State(Line x, PeriodStatus status) => status != PeriodStatus.Paid
        ? LiveState(x)
        : x.Manual is not null ? LinePaymentState.PaidManually
        : x.Entity.PaymentStatus is PaymentRowStatus.MissingAccount or PaymentRowStatus.InvalidAccount && LiveState(x) == LinePaymentState.Ready
            ? LinePaymentState.Ready
        : x.Entity.PaymentStatus switch
        {
            null => LinePaymentState.NothingDue,
            PaymentRowStatus.Pending => LinePaymentState.Pending,
            PaymentRowStatus.Paid => LinePaymentState.Paid,
            PaymentRowStatus.Rejected => LinePaymentState.Rejected,
            PaymentRowStatus.InvalidAccount => LinePaymentState.InvalidAccount,
            _ => x.CardRequested ? LinePaymentState.CardRequested : LinePaymentState.MissingAccount,
        };

    /// <summary>
    /// A paid period's line still owed (no account, an invalid one, or rejected by the bank) can be recorded as paid outside the files.
    /// </summary>
    public static bool CanPayManually(Line x, PeriodStatus status) =>
        status == PeriodStatus.Paid && x.Manual is null && x.Entity.TotalNet > 0 && x.Entity.PaymentStatus is { } s && Unpaid.Contains(s);

    /// <summary>The lines the file that can be generated now would pay.</summary>
    public static IEnumerable<Line> NextFile(IEnumerable<Line> lines, PeriodStatus status) => status switch
    {
        PeriodStatus.InReview => lines.Where(x => LiveState(x) == LinePaymentState.Ready),
        PeriodStatus.Paid => lines.Where(x => x.Entity.PaymentStatus is { } s && Unpaid.Contains(s) && LiveState(x) == LinePaymentState.Ready),
        _ => [],
    };

    public static AccountType LayoutType(BankAccountType type) => type switch
    {
        BankAccountType.DebitCard => AccountType.DebitCard,
        BankAccountType.Clabe => AccountType.Clabe,
        _ => AccountType.OwnAccount,
    };

    public static PaymentType PaymentTypeFor(string bankCode) =>
        AztecaMassPayment.PaymentType(bankCode) == "3" ? PaymentType.BazTransfer : PaymentType.Spei;

    public static async Task<List<PaymentBatchDto>> BatchesAsync(DbContext db, Guid periodId, CancellationToken ct)
    {
        var batches = await db.Set<PaymentBatch>().AsNoTracking().Include(b => b.Rows).Where(b => b.PayrollPeriodId == periodId)
            .OrderBy(b => b.GeneratedAt).ToListAsync(ct);
        var lineIds = batches.SelectMany(b => b.Rows).Select(r => r.PrepayrollLineId).Distinct().ToList();
        var names = await db.Set<PrepayrollLine>().Where(l => lineIds.Contains(l.Uid))
            .Join(db.Set<Employee>(), l => l.EmployeeId, e => e.Uid, (l, e) => new { l.Uid, e.PersonId })
            .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.Uid, p.FullName })
            .ToDictionaryAsync(x => x.Uid, x => x.FullName, ct);
        var banks = await db.Set<BankRow>().AsNoTracking().GroupBy(b => b.SpeiCode).Select(g => new { g.Key, Name = g.Min(b => b.Name) })
            .ToDictionaryAsync(x => x.Key, x => x.Name!, ct);
        var userIds = batches.Select(b => b.GeneratedBy).Concat(batches.Where(b => b.UploadedBy != null).Select(b => b.UploadedBy!.Value)).Distinct().ToList();
        var users = await db.Set<User>().Where(u => userIds.Contains(u.Uid)).ToDictionaryAsync(u => u.Uid, u => u.DisplayName, ct);
        return batches.Select(b => new PaymentBatchDto(b.Uid, b.Kind, b.Status, b.GeneratedAt, users[b.GeneratedBy], b.UploadedAt,
                b.UploadedBy is { } u ? users[u] : null, b.RowCount, b.TotalAmount, b.FrequentAccountsCount,
                b.Rows.OrderBy(r => r.LineNo).Select(r => new PaymentBatchRowDto(r.Uid, r.LineNo, r.PrepayrollLineId, names[r.PrepayrollLineId],
                    r.DisplayName, banks.GetValueOrDefault(r.ReceivingBankCode, r.ReceivingBankCode), r.AccountLast4, r.AccountType, r.PaymentType,
                    r.Amount, r.Status, r.RejectionReason, r.RegistersFrequentAccount)).ToList()))
            .ToList();
    }

    public static PayingAccountDto Dto(PayingAccount a) =>
        new(a.Uid, a.Name, a.ChargeAccountLast4, a.HolderName, a.HolderRfc, a.DefaultChargeLegend, a.DefaultCreditLegend, a.Currency);
}
