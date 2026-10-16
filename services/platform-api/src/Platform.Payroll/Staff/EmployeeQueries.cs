using Microsoft.EntityFrameworkCore;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Payroll.Groups;

namespace Platform.Payroll.Staff;

/// <summary>Read side of Personal: list rows and the detail, statuses derived for «today» (America/Monterrey).</summary>
public static class EmployeeQueries
{
    /// <summary>The default «Empleado» document template (SPEC §5.1 «Documents: template Empleado»).</summary>
    public static Task<Guid?> EmployeeTemplate(DbContext db, CancellationToken ct) =>
        db.Set<DocumentTemplate>().Where(t => t.Scope == DocumentTemplateScope.Employee && t.IsDefault).Select(t => (Guid?)t.Uid)
            .FirstOrDefaultAsync(ct);

    public static async Task<List<EmployeeListItemDto>> List(DbContext db, IQueryable<Employee> q, DateOnly today, CancellationToken ct)
    {
        var template = await EmployeeTemplate(db, ct);
        var rows = await q
            .Join(db.Set<Person>(), e => e.PersonId, p => p.Uid, (e, p) => new { e, p.FullName })
            .Join(db.Set<PayrollGroup>(), x => x.e.PayrollGroupId, g => g.Uid, (x, g) => new { x.e, x.FullName, Group = g.Name, g.ClientId })
            .Join(db.Set<Client>(), x => x.ClientId, c => c.Uid, (x, c) => new { x.e, x.FullName, x.Group, x.ClientId, Client = c.ShortName })
            .Join(db.Set<Position>(), x => x.e.PositionId, p => p.Uid, (x, p) => new { x.e, x.FullName, x.Group, x.ClientId, x.Client, Position = p.Name })
            .Select(x => new
            {
                x.e,
                x.FullName,
                x.Group,
                x.ClientId,
                x.Client,
                x.Position,
                HasAccount = db.Set<PersonBankAccount>().Any(a => a.PersonId == x.e.PersonId && a.IsPrimary && (a.ValidTo == null || a.ValidTo >= today)),
                Card = db.Set<CardRequest>().Where(r => r.EmployeeId == x.e.Uid
                        && (r.Status == CardRequestStatus.Queued || r.Status == CardRequestStatus.InBatch || r.Status == CardRequestStatus.Uploaded))
                    .Select(r => new CardRequestDto(r.Uid, r.EmployeeId, r.Bank, r.Status, r.RequestedAt)).FirstOrDefault(),
            })
            .OrderBy(x => x.FullName).AsNoTracking().ToListAsync(ct);
        var ids = rows.Select(r => r.e.Uid).ToList();
        var periods = await db.Set<EmployeeStatusPeriod>().AsNoTracking().Where(p => ids.Contains(p.EmployeeId)).ToListAsync(ct);
        var completeness = await Completeness(db, rows.Select(r => r.e.PersonId).Distinct().ToList(), template, ct);
        return rows.Select(r =>
            {
                var own = periods.Where(p => p.EmployeeId == r.e.Uid).ToList();
                var current = StaffRules.Covering(own, today);
                return new EmployeeListItemDto(r.e.Uid, r.e.PersonId, r.FullName, r.ClientId, r.Client, r.e.PayrollGroupId, r.Group,
                    r.e.PositionId, r.Position, r.e.Shift, r.e.HiredAt, r.e.TerminatedAt, current?.Status ?? EmployeeStatus.Active,
                    Dto(current ?? StaffRules.Next(own, today)), r.HasAccount, r.Card, completeness.GetValueOrDefault(r.e.PersonId));
            })
            .ToList();
    }

    public static async Task<EmployeeDetailDto?> Detail(DbContext db, IQueryable<Employee> scope, Guid id, DateOnly today, CancellationToken ct)
    {
        var e = await scope.AsNoTracking().SingleOrDefaultAsync(x => x.Uid == id, ct);
        if (e is null)
            return null;
        var person = await db.Set<Person>().AsNoTracking().SingleAsync(p => p.Uid == e.PersonId, ct);
        var group = await db.Set<PayrollGroup>().AsNoTracking().SingleAsync(g => g.Uid == e.PayrollGroupId, ct);
        var client = await db.Set<Client>().Where(c => c.Uid == group.ClientId).Select(c => c.ShortName).SingleAsync(ct);
        var position = await db.Set<Position>().Where(p => p.Uid == e.PositionId).Select(p => p.Name).SingleAsync(ct);
        var rates = await StaffRules.PositionRatesOn(db, e.PositionId, today, ct);
        var periods = await db.Set<EmployeeStatusPeriod>().AsNoTracking().Where(p => p.EmployeeId == id).ToListAsync(ct);
        var current = StaffRules.Covering(periods, today);
        var hasAccount = await StaffRules.HasAccountAsync(db, e.PersonId, today, ct);
        var card = await db.Set<CardRequest>().AsNoTracking()
            .Where(r => r.EmployeeId == id && (r.Status == CardRequestStatus.Queued || r.Status == CardRequestStatus.InBatch
                || r.Status == CardRequestStatus.Uploaded))
            .Select(r => new CardRequestDto(r.Uid, r.EmployeeId, r.Bank, r.Status, r.RequestedAt)).FirstOrDefaultAsync(ct);
        return new EmployeeDetailDto(e.Uid, person.Uid, person.FullName, person.Curp, person.Rfc, person.Nss, person.MobilePhone,
            person.Email, person.IsForeign, group.ClientId, client, group.Uid, group.Name, e.PositionId, position, e.Shift, e.HiredAt,
            e.TerminatedAt, current?.Status ?? EmployeeStatus.Active, Dto(current), Dto(StaffRules.Next(periods, today)),
            e.DailySalaryPocketOverride, e.DailySalaryImssOverride, e.HourlyRateOverride, e.DaysWorkedBonusOverride,
            rates is null ? null : StaffRules.Rates(rates), StaffRules.Effective(e, rates), e.Notes, e.CandidateId,
            await EmployeeTemplate(db, ct), hasAccount, card);
    }

    public static StatusPeriodDto? Dto(EmployeeStatusPeriod? p) =>
        p is null ? null : new StatusPeriodDto(p.Uid, p.Status, p.StartDate, p.EndDate, p.Reason, p.Reference);

    private static async Task<Dictionary<Guid, decimal>> Completeness(DbContext db, List<Guid> people, Guid? template, CancellationToken ct) =>
        template is not { } t || people.Count == 0
            ? []
            : await db.Database.SqlQuery<CompletenessRow>(
                    $"SELECT person_id, completeness FROM platform.person_document_completeness WHERE template_id = {t} AND person_id = ANY({people.ToArray()})")
                .ToDictionaryAsync(r => r.PersonId, r => r.Completeness, ct);

    private sealed record CompletenessRow(Guid PersonId, decimal Completeness);
}
