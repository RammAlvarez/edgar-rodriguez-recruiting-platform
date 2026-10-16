using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Platform.Core.Catalogs;
using Platform.Payroll.Engine;
using Platform.Payroll.Groups;
using Platform.Payroll.Periods;
using Platform.Payroll.Staff;

namespace Platform.Payroll.Prepayroll;

/// <summary>
/// The live prenómina (SPEC §5.5, D38, D84): every line of an open period (Capturando or Revisión) is recomputed by the engine
/// from attendance, incidents, the rates valid at the period's end and the group's rules, in the same transaction as the write
/// that changed them. A paid period's lines never change (S10; a trigger enforces it).
/// </summary>
public static class Prepayrolls
{
    private static readonly PeriodStatus[] Open = [PeriodStatus.Capturing, PeriodStatus.InReview];

    /// <summary>
    /// Saves the pending source changes and recomputes the lines they touch, in one transaction: the recompute reads the
    /// database, so sources are saved first. <paramref name="scope"/> = (employee, first date, last date).
    /// </summary>
    public static async Task SaveAndRecomputeAsync(DbContext db, IEnumerable<(Guid EmployeeId, DateOnly From, DateOnly To)> scope,
        DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.SaveChangesAsync(ct);
        foreach (var (employeeId, from, to) in scope.Distinct())
        {
            var groupId = await db.Set<Employee>().Where(e => e.Uid == employeeId).Select(e => e.PayrollGroupId).SingleAsync(ct);
            var periods = await db.Set<PayrollPeriod>()
                .Where(p => p.PayrollGroupId == groupId && Open.Contains(p.Status) && p.StartDate <= to && p.EndDate >= from)
                .Select(p => p.Uid).ToListAsync(ct);
            foreach (var period in periods)
                await RecomputeAsync(db, period, employeeId, now, ct);
        }
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// After an alta, a reingreso, a change of rates / turno / group: saves, then recomputes the employee's lines in every open
    /// period — creating them in the current group's periods and removing those left in another group's.
    /// </summary>
    public static async Task SaveAndRefreshEmployeeAsync(DbContext db, Guid employeeId, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.SaveChangesAsync(ct);
        var groupId = await db.Set<Employee>().Where(e => e.Uid == employeeId).Select(e => e.PayrollGroupId).SingleAsync(ct);
        var periods = await db.Set<PayrollPeriod>()
            .Where(p => Open.Contains(p.Status) && (p.PayrollGroupId == groupId
                || db.Set<PrepayrollLine>().Any(l => l.PayrollPeriodId == p.Uid && l.EmployeeId == employeeId)))
            .Select(p => p.Uid).ToListAsync(ct);
        foreach (var period in periods)
            await RecomputeAsync(db, period, employeeId, now, ct);
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
    }

    /// <summary>Recomputes every line of the group's open periods (rates, rules or concepts changed).</summary>
    public static async Task RecomputeGroupAsync(DbContext db, Guid groupId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var period in await db.Set<PayrollPeriod>().Where(p => p.PayrollGroupId == groupId && Open.Contains(p.Status)).ToListAsync(ct))
            await EnsureLinesAsync(db, period, now, ct, recomputeAll: true);
    }

    /// <summary>
    /// Creates (and computes) the line of every employee who worked in the group during the period and has none yet; with
    /// <paramref name="recomputeAll"/> the existing ones are recomputed too. Returns how many lines were created.
    /// </summary>
    public static async Task<int> EnsureLinesAsync(DbContext db, PayrollPeriod period, DateTimeOffset now, CancellationToken ct,
        bool recomputeAll = false)
    {
        var employees = await db.Set<Employee>().AsNoTracking()
            .Where(e => e.PayrollGroupId == period.PayrollGroupId && e.HiredAt <= period.EndDate
                && (e.TerminatedAt == null || e.TerminatedAt > period.StartDate))
            .Select(e => e.Uid).ToListAsync(ct);
        var existing = await db.Set<PrepayrollLine>().Where(l => l.PayrollPeriodId == period.Uid).Select(l => l.EmployeeId).ToListAsync(ct);
        var created = 0;
        foreach (var employee in employees)
        {
            if (!existing.Contains(employee))
                created++;
            else if (!recomputeAll)
                continue;
            await RecomputeAsync(db, period.Uid, employee, now, ct);
        }
        return created;
    }

    /// <summary>Computes one employee's line in one period and replaces its items (or removes it if they did not work there).</summary>
    public static async Task RecomputeAsync(DbContext db, Guid periodId, Guid employeeId, DateTimeOffset now, CancellationToken ct)
    {
        var period = await db.Set<PayrollPeriod>().AsNoTracking().SingleAsync(p => p.Uid == periodId, ct);
        if (!Open.Contains(period.Status))
            return;
        var employee = await db.Set<Employee>().AsNoTracking().SingleAsync(e => e.Uid == employeeId, ct);
        var line = await db.Set<PrepayrollLine>().Include(l => l.Items)
            .SingleOrDefaultAsync(l => l.PayrollPeriodId == periodId && l.EmployeeId == employeeId, ct);
        var employed = employee.PayrollGroupId == period.PayrollGroupId && employee.HiredAt <= period.EndDate
            && (employee.TerminatedAt is not { } t || t > period.StartDate);
        if (!employed)
        {
            if (line is not null)
                db.Remove(line);
            return;
        }

        var group = await db.Set<PayrollGroup>().AsNoTracking().SingleAsync(g => g.Uid == period.PayrollGroupId, ct);
        var position = await db.Set<Position>().AsNoTracking().SingleAsync(p => p.Uid == employee.PositionId, ct);
        var positionRates = await StaffRules.PositionRatesOn(db, employee.PositionId, period.EndDate, ct);
        var rates = StaffRules.Effective(employee, positionRates);
        var days = await db.Set<AttendanceRecord>().AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.WorkDate >= period.StartDate && a.WorkDate <= period.EndDate).ToListAsync(ct);
        var holidays = (await db.Set<Holiday>().Where(h => h.Date >= period.StartDate && h.Date <= period.EndDate).Select(h => h.Date)
            .ToListAsync(ct)).ToHashSet();
        var concepts = await db.Set<PayConceptRow>().AsNoTracking().ToDictionaryAsync(c => c.Uid, ct);
        var codes = concepts.Values.ToDictionary(c => c.Code, c => c.Uid);
        var groupConcepts = await db.Set<PayrollGroupConcept>().AsNoTracking().Where(c => c.PayrollGroupId == group.Uid)
            .ToDictionaryAsync(c => concepts[c.PayConceptId].Code, ct);
        var incidents = await db.Set<Incident>().AsNoTracking()
            .Where(i => i.PayrollPeriodId == periodId && i.EmployeeId == employeeId).ToListAsync(ct);

        var summary = AttendanceSummaries.Summarize(days, holidays);
        var restDayAmount = groupConcepts.GetValueOrDefault(PayConcept.RestDayWorked.Info().Code)?.AmountOverride
            ?? concepts[codes[PayConcept.RestDayWorked.Info().Code]].DefaultAmount ?? 500m;
        var inputs = incidents.Select(i => new IncidentInput(Concept(concepts[i.PayConceptId].Code), i.Quantity, i.Amount,
                i.AdjustedConceptId is { } a ? Concept(concepts[a].Code) : null, i.Uid))
            .Concat(AttendanceSummaries.RestDaysWorked(days, restDayAmount)).ToList();
        var bonuses = Bonuses(groupConcepts, concepts, codes, rates);
        var result = rates is null
            ? new PrepayrollResult([], 0, 0, 0, 0)
            : PrepayrollCalculator.Calculate(new PrepayrollInput(
                new GroupRules(group.SeventhDayDivisor, group.SundayPremiumPct, group.HolidayWorkedMultiplier),
                new Rates(rates.DailySalaryPocket, rates.DailySalaryImss, rates.HourlyRate, rates.DaysWorkedBonus),
                summary, inputs, bonuses));

        if (line is null)
            db.Add(line = new PrepayrollLine { PayrollPeriodId = periodId, EmployeeId = employeeId, PositionName = "", Shift = "" });
        line.PositionName = position.Name;
        line.Shift = employee.Shift;
        line.HiredAt = employee.HiredAt;
        line.DaysWorked = summary.DaysWorked;
        line.HolidaysWorked = summary.HolidaysWorked;
        line.HolidaysEntitled = summary.HolidaysEntitled;
        line.PartialHours = summary.PartialHours;
        line.OvertimeHours = summary.OvertimeHours;
        line.OvertimeDoubleHours = incidents.Where(i => concepts[i.PayConceptId].Code == PayConcept.OvertimeDouble.Info().Code).Sum(i => i.Quantity);
        line.OvertimeTripleHours = incidents.Where(i => concepts[i.PayConceptId].Code == PayConcept.OvertimeTriple.Info().Code).Sum(i => i.Quantity);
        line.WorkedSunday = summary.WorkedSunday;
        line.DailySalaryPocket = rates?.DailySalaryPocket ?? 0;
        line.DailySalaryImss = rates?.DailySalaryImss ?? 0;
        line.HourlyRate = rates?.HourlyRate ?? 0;
        line.GrossEarnings = result.GrossEarnings;
        line.TotalDeductions = result.TotalDeductions;
        line.TotalNetExact = result.TotalNetExact;
        line.TotalNet = result.TotalNet;
        line.ComputedAt = now;
        db.RemoveRange(line.Items);
        line.Items.Clear();
        foreach (var item in result.Items)
            line.Items.Add(new PrepayrollLineItem
            {
                PayConceptId = codes[item.Concept.Info().Code],
                Quantity = item.Quantity,
                UnitValue = item.UnitValue,
                Amount = item.Amount,
                Source = item.Source,
                IncidentId = item.IncidentId,
            });
    }

    /// <summary>
    /// Capturando → Revisión (by the lock job at the last day's lock, or «Cerrar captura»): every uncaptured day an employee
    /// worked in the group becomes a locked automatic falta (SPEC §5.3), then every line is recomputed. Saves; returns the
    /// faltas written.
    /// </summary>
    public static async Task<int> CloseCaptureAsync(DbContext db, PayrollGroup group, PayrollPeriod period, DateTimeOffset now,
        CancellationToken ct)
    {
        var employees = await db.Set<Employee>().AsNoTracking()
            .Where(e => e.PayrollGroupId == group.Uid && e.HiredAt <= period.EndDate && (e.TerminatedAt == null || e.TerminatedAt > period.StartDate))
            .ToListAsync(ct);
        var ids = employees.Select(e => e.Uid).ToList();
        var captured = (await db.Set<AttendanceRecord>().AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.WorkDate >= period.StartDate && a.WorkDate <= period.EndDate)
                .Select(a => new { a.EmployeeId, a.WorkDate }).ToListAsync(ct))
            .Select(a => (a.EmployeeId, a.WorkDate)).ToHashSet();
        var created = 0;
        foreach (var e in employees)
            for (var d = period.StartDate; d <= period.EndDate; d = d.AddDays(1))
            {
                if (d < e.HiredAt || (e.TerminatedAt is { } t && d >= t) || captured.Contains((e.Uid, d)))
                    continue;
                db.Add(new AttendanceRecord
                {
                    EmployeeId = e.Uid,
                    WorkDate = d,
                    Status = AttendanceStatus.Absent,
                    IsAutoAbsent = true,
                    IsLocked = true,
                    RecordedAt = now,
                    Note = "Sin captura al cerrar el período",
                });
                created++;
            }
        var tracked = await db.Set<PayrollPeriod>().SingleAsync(p => p.Uid == period.Uid, ct);
        tracked.Status = PeriodStatus.InReview;
        tracked.ReviewStartedAt = now;
        await db.SaveChangesAsync(ct);
        await EnsureLinesAsync(db, tracked, now, ct, recomputeAll: true);
        await db.SaveChangesAsync(ct);
        return created;
    }

    public static PayConcept Concept(string code) => PayConcepts.All.Single(c => c.Code == code).Concept;

    /// <summary>The group's active bonuses (D63): BONO DÍAS TRABAJADOS defaults to the puesto's amount.</summary>
    private static List<GroupBonus> Bonuses(Dictionary<string, PayrollGroupConcept> groupConcepts, Dictionary<Guid, PayConceptRow> concepts,
        Dictionary<string, Guid> codes, EmployeeRatesDto? rates)
    {
        var list = new List<GroupBonus>();
        foreach (var bonus in new[] { PayConcept.BonusDaysWorked, PayConcept.BonusPunctuality, PayConcept.BonusProductivity })
        {
            var code = bonus.Info().Code;
            if (groupConcepts.GetValueOrDefault(code) is not { IsActive: true } gc)
                continue;
            var row = concepts[codes[code]];
            var amount = gc.AmountOverride ?? (bonus == PayConcept.BonusDaysWorked ? rates?.DaysWorkedBonus : row.DefaultAmount) ?? 0;
            var basis = row.Calc == ConceptCalc.PerDayWorked ? gc.RateOverride ?? row.DefaultRate : null;
            list.Add(new GroupBonus(bonus, amount, basis));
        }
        return list;
    }
}
