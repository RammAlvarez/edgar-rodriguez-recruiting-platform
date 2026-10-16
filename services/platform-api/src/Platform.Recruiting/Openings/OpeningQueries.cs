using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;

namespace Platform.Recruiting.Openings;

/// <summary>Read side of Vacantes: list rows, options and the detail, all named through <c>job_opening_display</c>.</summary>
public static class OpeningQueries
{
    public static async Task<Dictionary<Guid, string>> DisplayNames(DbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? []
            : await db.Database.SqlQuery<JobOpeningDisplay>(
                    $"SELECT id, display_name FROM platform.job_opening_display WHERE id = ANY({ids.ToArray()})")
                .ToDictionaryAsync(d => d.Id, d => d.DisplayName, ct);

    public static async Task<List<OpeningListItemDto>> List(DbContext db, IQueryable<JobOpening> q, IClock clock, CancellationToken ct)
    {
        var rows = await q.OrderByDescending(o => o.RequestedAt).ThenByDescending(o => o.Id)
            .Select(o => new
            {
                o,
                Client = db.Set<Client>().Where(c => c.Uid == o.ClientId).Select(c => c.ShortName).First(),
                Zone = db.Set<Zone>().Where(z => z.Uid == o.ZoneId).Select(z => z.NameEs).FirstOrDefault(),
                Lead = db.Set<User>().Where(u => u.Uid == o.LeadRecruiterId).Select(u => u.DisplayName).FirstOrDefault(),
                Candidates = db.Set<Candidate>().Count(c => c.JobOpeningId == o.Uid),
                Hired = db.Set<Candidate>().Count(c => c.JobOpeningId == o.Uid && c.IsHired),
            })
            .AsNoTracking().ToListAsync(ct);
        var names = await DisplayNames(db, rows.Select(r => r.o.Uid).ToList(), ct);
        var today = clock.Today();
        return rows.Select(r => new OpeningListItemDto(r.o.Uid, names[r.o.Uid], r.o.ClientId, r.Client, r.o.PositionName, r.o.ZoneId,
            r.Zone, r.o.HeadcountRequested, r.Hired, r.Candidates, r.o.Priority, r.o.Status, r.o.RequestedAt, r.o.FilledAt,
            r.o.DaysOpen(today), r.o.LeadRecruiterId, r.Lead)).ToList();
    }

    public static async Task<OpeningDetailDto?> Detail(DbContext db, IQueryable<JobOpening> scope, Guid id, IClock clock,
        CancellationToken ct)
    {
        var o = await scope.AsNoTracking().Include(x => x.Recruiters).SingleOrDefaultAsync(x => x.Uid == id, ct);
        if (o is null)
            return null;
        var client = await db.Set<Client>().Where(c => c.Uid == o.ClientId).Select(c => c.ShortName).SingleAsync(ct);
        var zone = o.ZoneId is { } z ? await db.Set<Zone>().Where(x => x.Uid == z).Select(x => x.NameEs).SingleOrDefaultAsync(ct) : null;
        var userIds = o.Recruiters.Select(r => r.UserId).ToList();
        var users = await db.Set<User>().Where(u => userIds.Contains(u.Uid)).ToDictionaryAsync(u => u.Uid, u => u.DisplayName, ct);
        var stages = await db.Set<Candidate>().Where(c => c.JobOpeningId == o.Uid).GroupBy(c => c.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Stage, g => g.Count, ct);
        var name = (await DisplayNames(db, [o.Uid], ct))[o.Uid];
        return new OpeningDetailDto(o.Uid, name, o.ClientId, client, o.ZoneId, zone, o.PositionName, o.HeadcountRequested,
            stages.GetValueOrDefault(CandidateStage.Hired), o.MonthlySalary, o.Benefits, o.Schedule, o.PaymentBankIds,
            o.EducationLevelId, o.YearsExperience, o.RequiredExperience, o.GenderRequirement, o.AgeRange, o.Priority, o.Status,
            o.RequestedAt, o.FilledAt, o.DaysOpen(clock.Today()), o.LeadRecruiterId,
            o.LeadRecruiterId is { } lead ? users.GetValueOrDefault(lead) : null, o.DocumentTemplateId, o.VisibleToClient, o.Notes,
            o.Recruiters.OrderByDescending(r => r.IsLead).ThenBy(r => users.GetValueOrDefault(r.UserId))
                .Select(r => new OpeningRecruiterDto(r.UserId, users.GetValueOrDefault(r.UserId) ?? "", r.IsLead, r.AssignedAt)).ToList(),
            Enum.GetValues<CandidateStage>().Select(s => new StageCountDto(s, stages.GetValueOrDefault(s))).ToList());
    }
}
