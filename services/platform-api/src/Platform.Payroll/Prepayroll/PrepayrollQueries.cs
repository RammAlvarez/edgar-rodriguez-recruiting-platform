using Microsoft.EntityFrameworkCore;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Staff;

namespace Platform.Payroll.Prepayroll;

/// <summary>Read side of Prenómina: the period list, the grid in the client's layout and a row's desglose.</summary>
public static class PrepayrollQueries
{
    /// <summary>The columns of SEM 38 (K–U), always shown; the others appear when a row has an amount in them.</summary>
    private static readonly HashSet<PayConcept> AlwaysShown =
    [
        PayConcept.Salary, PayConcept.SeventhDay, PayConcept.SundayPremium, PayConcept.PartialHours, PayConcept.HolidayFirstHalf,
        PayConcept.Overtime, PayConcept.Retroactive, PayConcept.RestDayWorked, PayConcept.OvertimeDouble, PayConcept.HolidayWorked,
        PayConcept.Deduction,
    ];

    /// <summary>Every amount column in the export order (the <see cref="PayConcept"/> order), DESCUENTO once.</summary>
    public static IReadOnlyList<string> AllColumns { get; } = Enum.GetValues<PayConcept>()
        .Where(c => c != PayConcept.ManualAdjustment).Select(c => c.Info().ExportColumn).Distinct().ToList();

    /// <summary>The concept an ajuste manual of <paramref name="column"/> adjusts (the first exported there).</summary>
    public static PayConcept? ConceptOfColumn(string column) =>
        Enum.GetValues<PayConcept>().Where(c => c != PayConcept.ManualAdjustment && c.Info().ExportColumn == column)
            .Select(c => (PayConcept?)c).FirstOrDefault();

    public static async Task<List<PayrollPeriodDto>> Periods(DbContext db, IQueryable<PayrollGroup> groups, IQueryable<PayrollPeriod> q,
        CancellationToken ct) =>
        await q.Join(groups, p => p.PayrollGroupId, g => g.Uid, (p, g) => new { p, g })
            .Join(db.Set<Client>(), x => x.g.ClientId, c => c.Uid, (x, c) => new { x.p, x.g, Client = c.ShortName })
            .OrderByDescending(x => x.p.StartDate).ThenBy(x => x.g.Name)
            .Select(x => new PayrollPeriodDto(x.p.Uid, x.g.Uid, x.g.Name, x.g.ClientId, x.Client, x.g.PeriodType == PeriodType.Weekly, x.p.PeriodNumber, x.p.Year, x.p.StartDate,
                x.p.EndDate, x.p.Status, x.p.ReviewStartedAt,
                db.Set<PrepayrollLine>().Count(l => l.PayrollPeriodId == x.p.Uid),
                x.p.TotalNet ?? db.Set<PrepayrollLine>().Where(l => l.PayrollPeriodId == x.p.Uid).Sum(l => (decimal?)l.TotalNet) ?? 0))
            .AsNoTracking().ToListAsync(ct);

    public static async Task<PrepayrollGridDto?> Grid(DbContext db, IQueryable<PayrollGroup> groups, Guid periodId, DateOnly today,
        CancellationToken ct)
    {
        var period = (await Periods(db, groups, db.Set<PayrollPeriod>().Where(p => p.Uid == periodId), ct)).SingleOrDefault();
        if (period is null)
            return null;
        var schedule = await db.Set<PayrollGroup>().Where(g => g.Uid == period.GroupId).Select(g => g.ScheduleDescription).SingleAsync(ct);
        var lines = await db.Set<PrepayrollLine>().AsNoTracking().Include(l => l.Items).Where(l => l.PayrollPeriodId == periodId)
            .Join(db.Set<Employee>(), l => l.EmployeeId, e => e.Uid, (l, e) => new { l, e.PersonId })
            .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.l, x.PersonId, p.FullName })
            .OrderBy(x => x.FullName).ToListAsync(ct);
        var pivot = await Pivot.LoadAsync(db, lines.Select(x => x.l), ct);
        var payments = await Payments(db, lines.Select(x => (x.l, x.PersonId, x.FullName)).ToList(), period.Status == PeriodStatus.Paid, today, ct);

        var cells = lines.Select(x => pivot.Cells(x.l)).ToList();
        var columns = Enum.GetValues<PayConcept>().Where(c => c != PayConcept.ManualAdjustment)
            .Where(c => AlwaysShown.Contains(c) || cells.Any(r => r.ContainsKey(c.Info().ExportColumn)))
            .Select(c => c.Info().ExportColumn).Distinct().ToList();
        var rows = lines.Select((x, i) => new PrepayrollRowDto(x.l.Uid, x.l.EmployeeId, x.FullName, x.l.HiredAt, x.l.PositionName, x.l.Shift,
                x.l.DaysWorked, x.l.HourlyRate, x.l.HolidaysWorked, x.l.DailySalaryImss, x.l.DailySalaryPocket,
                columns.Select(c => cells[i].TryGetValue(c, out var v) ? PrepayrollCalculator.TruncateToCents(v) : (decimal?)null).ToList(),
                x.l.TotalNet, x.l.TotalNetExact, x.l.Comments, x.l.Items.Any(it => it.Source == ItemSource.Manual), payments[x.l.EmployeeId]))
            .ToList();
        var footers = columns.Select((_, c) => rows.Sum(r => r.Amounts[c] ?? 0)).ToList();
        return new PrepayrollGridDto(period, schedule, period.Status != PeriodStatus.Paid, columns, rows, footers,
            rows.Sum(r => r.TotalNet), rows.Sum(r => r.TotalNetExact));
    }

    public static async Task<PrepayrollLineDetailDto?> Detail(DbContext db, IQueryable<PayrollGroup> groups, Guid lineId, CancellationToken ct)
    {
        var x = await db.Set<PrepayrollLine>().AsNoTracking().Include(l => l.Items).Where(l => l.Uid == lineId)
            .Join(db.Set<PayrollPeriod>(), l => l.PayrollPeriodId, p => p.Uid, (l, p) => new { l, p })
            .Where(x => groups.Any(g => g.Uid == x.p.PayrollGroupId))
            .Join(db.Set<Employee>(), x => x.l.EmployeeId, e => e.Uid, (x, e) => new { x.l, x.p, e.PersonId })
            .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.l, x.p, p.FullName })
            .SingleOrDefaultAsync(ct);
        if (x is null)
            return null;
        var concepts = await db.Set<PayConceptRow>().AsNoTracking().ToDictionaryAsync(c => c.Uid, ct);
        var pivot = await Pivot.LoadAsync(db, [x.l], ct);
        var incidents = await Incidents(db, db.Set<Incident>().Where(i => i.PayrollPeriodId == x.p.Uid && i.EmployeeId == x.l.EmployeeId), ct);
        var reasons = incidents.ToDictionary(i => i.Id, i => i.Reason);
        var holidays = (await db.Set<Holiday>().Where(h => h.Date >= x.p.StartDate && h.Date <= x.p.EndDate).Select(h => h.Date).ToListAsync(ct))
            .ToHashSet();
        var days = await db.Set<AttendanceRecord>().AsNoTracking()
            .Where(a => a.EmployeeId == x.l.EmployeeId && a.WorkDate >= x.p.StartDate && a.WorkDate <= x.p.EndDate).OrderBy(a => a.WorkDate)
            .ToListAsync(ct);
        return new PrepayrollLineDetailDto(x.l.Uid, x.p.Uid, x.p.Status, x.l.EmployeeId, x.FullName, x.l.PositionName, x.l.Shift, x.l.DaysWorked,
            x.l.HolidaysEntitled, x.l.HolidaysWorked, x.l.PartialHours, x.l.OvertimeHours, x.l.WorkedSunday, x.l.DailySalaryPocket,
            x.l.DailySalaryImss, x.l.HourlyRate,
            x.l.Items.OrderBy(i => concepts[i.PayConceptId].SortOrder).ThenBy(i => i.Id)
                .Select(i => new LineItemDto(concepts[i.PayConceptId].Code, concepts[i.PayConceptId].NameEs, pivot.Column(i),
                    concepts[i.PayConceptId].Kind, i.Quantity, i.UnitValue, i.Amount, i.Source, i.IncidentId,
                    i.IncidentId is { } id ? reasons.GetValueOrDefault(id) : null))
                .ToList(),
            days.Select(d => new LineDayDto(d.WorkDate, d.Status, d.HoursWorked, d.OvertimeHours, d.IsAutoAbsent, holidays.Contains(d.WorkDate)))
                .ToList(),
            incidents, x.l.GrossEarnings, x.l.TotalDeductions, x.l.TotalNetExact, x.l.TotalNet, x.l.Comments, x.l.ComputedAt);
    }

    /// <summary>Every line of the period as a receipt, by name; an ajuste manual prints as «Ajuste: SUELDO».</summary>
    public static async Task<List<Receipt>> Receipts(DbContext db, Guid periodId, DateOnly today, CancellationToken ct)
    {
        var paid = await db.Set<PayrollPeriod>().AnyAsync(p => p.Uid == periodId && p.Status == PeriodStatus.Paid, ct);
        var lines = await db.Set<PrepayrollLine>().AsNoTracking().Include(l => l.Items).Where(l => l.PayrollPeriodId == periodId)
            .Join(db.Set<Employee>(), l => l.EmployeeId, e => e.Uid, (l, e) => new { l, e.PersonId })
            .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.l, x.PersonId, p.FullName })
            .OrderBy(x => x.FullName).ToListAsync(ct);
        var concepts = await db.Set<PayConceptRow>().AsNoTracking().ToDictionaryAsync(c => c.Uid, ct);
        var pivot = await Pivot.LoadAsync(db, lines.Select(x => x.l), ct);
        var payments = await Payments(db, lines.Select(x => (x.l, x.PersonId, x.FullName)).ToList(), paid, today, ct);
        return lines.Select(x =>
            {
                var items = x.l.Items.OrderBy(i => concepts[i.PayConceptId].SortOrder).ThenBy(i => i.Id)
                    .Select(i => (Item: new ReceiptItem(i.Source == ItemSource.Manual ? $"Ajuste: {pivot.Column(i)}" : concepts[i.PayConceptId].NameEs,
                        i.Quantity, PrepayrollLabels.Cents(pivot.Signed(i))), Deduction: concepts[i.PayConceptId].Kind == ConceptKind.Deduction))
                    .ToList();
                return new Receipt(x.FullName, x.l.PositionName, x.l.Shift, x.l.DaysWorked, items.Where(i => !i.Deduction).Select(i => i.Item).ToList(),
                    items.Where(i => i.Deduction).Select(i => i.Item).ToList(), x.l.TotalNet, payments[x.l.EmployeeId], x.l.Comments);
            })
            .ToList();
    }

    public static async Task<List<IncidentDto>> Incidents(DbContext db, IQueryable<Incident> q, CancellationToken ct)
    {
        var rows = await q
            .Join(db.Set<Employee>(), i => i.EmployeeId, e => e.Uid, (i, e) => new { i, e.PersonId })
            .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.i, p.FullName })
            .Join(db.Set<PayrollPeriod>(), x => x.i.PayrollPeriodId, p => p.Uid, (x, p) => new { x.i, x.FullName, Period = p })
            .Join(db.Set<PayConceptRow>(), x => x.i.PayConceptId, c => c.Uid, (x, c) => new { x.i, x.FullName, x.Period, Concept = c })
            .Join(db.Set<User>(), x => x.i.ReportedBy, u => u.Uid, (x, u) => new { x.i, x.FullName, x.Period, x.Concept, By = u.DisplayName })
            .Select(x => new
            {
                x.i,
                x.FullName,
                x.Period,
                x.Concept,
                x.By,
                Origin = db.Set<PayrollPeriod>().Where(p => p.Uid == x.i.OriginPeriodId).Select(p => (int?)p.PeriodNumber).FirstOrDefault(),
                Adjusted = db.Set<PayConceptRow>().Where(c => c.Uid == x.i.AdjustedConceptId).Select(c => c.ExportColumn).FirstOrDefault(),
            })
            .OrderByDescending(x => x.i.IncidentDate).ThenByDescending(x => x.i.CreatedAt).AsNoTracking().ToListAsync(ct);
        return rows.Select(x => new IncidentDto(x.i.Uid, x.i.EmployeeId, x.FullName, x.Period.Uid, x.Period.PeriodNumber, x.i.OriginPeriodId,
                x.Origin, x.Concept.Code, x.Concept.NameEs, x.Adjusted, x.i.IncidentDate, x.i.Quantity, x.i.Amount, x.i.Reason, x.i.DuringReview,
                x.By, x.i.CreatedAt, x.Period.Status != PeriodStatus.Paid))
            .ToList();
    }

    /// <summary>The signed exact Σ of each column of a line (what an ajuste manual moves from).</summary>
    public static async Task<Dictionary<string, decimal>> ColumnTotals(DbContext db, PrepayrollLine line, CancellationToken ct)
    {
        return (await Pivot.LoadAsync(db, [line], ct)).Cells(line);
    }

    /// <summary>
    /// Open period: live — the person's current principal account, else a pending card request, else nothing. Paid period: the
    /// account the line was paid to (its snapshot) and the state its payment files left it in.
    /// </summary>
    private static async Task<Dictionary<Guid, LinePaymentDto>> Payments(DbContext db,
        List<(PrepayrollLine Line, Guid PersonId, string FullName)> who, bool paid, DateOnly today, CancellationToken ct)
    {
        var people = who.Select(w => w.PersonId).Distinct().ToList();
        var employees = who.Select(w => w.Line.EmployeeId).ToList();
        var banks = await db.Set<Bank>().AsNoTracking().ToDictionaryAsync(b => b.Uid, b => b.Name, ct);
        var accounts = (await db.Set<PersonBankAccount>().AsNoTracking()
                .Where(a => people.Contains(a.PersonId) && a.IsPrimary && (a.ValidTo == null || a.ValidTo >= today))
                .ToListAsync(ct))
            .GroupBy(a => a.PersonId).ToDictionary(g => g.Key, g => g.First());
        var cards = (await db.Set<CardRequest>().AsNoTracking()
                .Where(r => employees.Contains(r.EmployeeId)
                    && (r.Status == CardRequestStatus.Queued || r.Status == CardRequestStatus.InBatch || r.Status == CardRequestStatus.Uploaded))
                .Select(r => r.EmployeeId).ToListAsync(ct))
            .ToHashSet();
        return who.ToDictionary(w => w.Line.EmployeeId, w =>
        {
            var l = w.Line;
            string? Third(string? holder) => holder is null || SameName(holder, w.FullName) ? null : holder;
            if (paid && l.BankId is { } bank)
                return new LinePaymentDto(State(l.PaymentStatus, false), banks[bank], l.AccountLast4, l.AccountType, Third(l.HolderName));
            if (paid)
                return new LinePaymentDto(State(l.PaymentStatus, cards.Contains(l.EmployeeId)), null, null, null, null);
            return accounts.TryGetValue(w.PersonId, out var a)
                ? new LinePaymentDto(PaymentState.Ready, banks[a.BankId], a.AccountLast4, a.AccountType, Third(a.HolderName))
                : new LinePaymentDto(cards.Contains(l.EmployeeId) ? PaymentState.CardRequested : PaymentState.MissingAccount, null, null, null, null);
        });
    }

    private static PaymentState State(PaymentRowStatus? status, bool cardRequested) => status switch
    {
        null => PaymentState.NothingDue,
        PaymentRowStatus.Pending => PaymentState.Pending,
        PaymentRowStatus.Paid => PaymentState.Paid,
        PaymentRowStatus.Rejected => PaymentState.Rejected,
        PaymentRowStatus.InvalidAccount => PaymentState.InvalidAccount,
        PaymentRowStatus.Ready => PaymentState.Ready,
        _ => cardRequested ? PaymentState.CardRequested : PaymentState.MissingAccount,
    };

    /// <summary>
    /// The holder is the employee when the same words appear, in any order, case or accents («NOE ALANIS TREVINO» is
    /// «ALANÍS TREVIÑO NOÉ»).
    /// </summary>
    public static bool SameName(string holder, string fullName)
    {
        static string[] Words(string s) => new string(s.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
        return Words(holder).SequenceEqual(Words(fullName));
    }
}

/// <summary>
/// Item → Excel column: the concept's, or for an ajuste manual the adjusted concept's (D63); amounts signed toward net pay
/// (a deduction is negative, an ajuste manual is already signed).
/// </summary>
internal sealed class Pivot(Dictionary<Guid, PayConceptRow> concepts, Dictionary<Guid, Guid> adjusted)
{
    public static async Task<Pivot> LoadAsync(DbContext db, IEnumerable<PrepayrollLine> lines, CancellationToken ct)
    {
        var concepts = await db.Set<PayConceptRow>().AsNoTracking().ToDictionaryAsync(c => c.Uid, ct);
        var incidentIds = lines.SelectMany(l => l.Items).Where(i => i.Source == ItemSource.Manual && i.IncidentId != null)
            .Select(i => i.IncidentId!.Value).Distinct().ToList();
        var adjusted = await db.Set<Incident>().AsNoTracking().Where(i => incidentIds.Contains(i.Uid) && i.AdjustedConceptId != null)
            .ToDictionaryAsync(i => i.Uid, i => i.AdjustedConceptId!.Value, ct);
        return new Pivot(concepts, adjusted);
    }

    public string Column(PrepayrollLineItem item) =>
        (item.Source == ItemSource.Manual && item.IncidentId is { } id && adjusted.TryGetValue(id, out var c)
            ? concepts[c].ExportColumn
            : concepts[item.PayConceptId].ExportColumn) ?? "";

    public decimal Signed(PrepayrollLineItem item) => concepts[item.PayConceptId].Kind == ConceptKind.Deduction ? -item.Amount : item.Amount;

    /// <summary>Σ of the exact items of each column of the line.</summary>
    public Dictionary<string, decimal> Cells(PrepayrollLine line) =>
        line.Items.GroupBy(Column).ToDictionary(g => g.Key, g => g.Sum(Signed));
}
