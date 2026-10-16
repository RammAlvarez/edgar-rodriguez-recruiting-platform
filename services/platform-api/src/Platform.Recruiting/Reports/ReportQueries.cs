using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Recruiting.Reports;

/// <summary>
/// The six reports over the views of DATA_MODEL §4.7 (PLAN D80 for what each range and filter selects). The views give counts
/// and dates; rates and averages are computed here once, with <see cref="ReportMath"/>, and reused by the exports.
/// </summary>
public static class ReportQueries
{
    /// <summary>Report 3 flags a vacante «sin actividad» after more than this many days without a movement (prototype).</summary>
    public const int StaleAfterDays = 7;

    /// <summary>The stages of the funnel, in order; No apto and Descartado are where candidates are lost.</summary>
    public static readonly IReadOnlyList<CandidateStage> FunnelStages =
        [CandidateStage.New, CandidateStage.Contacted, CandidateStage.Interview, CandidateStage.Suitable, CandidateStage.Hired];

    // ---- view rows (columns by the snake_case naming convention) ----

    private sealed record PerformanceRow(Guid RecruiterId, Guid JobOpeningId, Guid ClientId, string PositionName, bool IsLead,
        DateOnly AssignedAt, DateOnly RequestedAt, DateOnly? FilledAt, OpeningStatus Status, int HeadcountRequested,
        long CandidatesPresented, long CandidatesSuitable, long CandidatesHired, int? DaysToFill);

    private sealed record SummaryRow(Guid ClientId, int OpeningsTotal, int OpeningsOpen, int OpeningsInProgress, int OpeningsFilled,
        int OpeningsCancelled, int OpeningsPaused, int CandidatesTotal, int CandidatesSuitable, int CandidatesHired, int DaysToFillTotal);

    private sealed record AgingRow(Guid Id, Guid ClientId, string PositionName, OpeningPriority Priority, OpeningStatus Status,
        int HeadcountRequested, DateOnly RequestedAt, Guid? LeadRecruiterId, long Hired, long StageNew, long StageContacted,
        long StageInterview, long StageSuitable, DateTimeOffset LastActivityAt);

    private sealed record FunnelRow(Guid CandidateId, Guid JobOpeningId, Guid ClientId, Guid? RecruiterId, CandidateSource? Source,
        CandidateStage Stage, DateTimeOffset ChangedAt, string? Note, CandidateStage? NextStage, DateTimeOffset? NextChangedAt);

    private sealed record SourceRow(Guid CandidateId, CandidateSource? Source, Guid ClientId, Guid? RecruiterId, DateOnly CapturedAt,
        bool? IsSuitable, bool IsHired, int? DaysToHire);

    private sealed record ClientOpeningRow(Guid ClientId, Guid JobOpeningId, DateOnly RequestedAt, OpeningStatus Status,
        int HeadcountRequested, DateOnly? FilledAt, long CandidatesPresented, long CandidatesSuitable, long Hired, int? DaysToFill);

    // ---- 1 Rendimiento por reclutador ----

    public static async Task<RecruiterPerformanceReportDto> RecruiterPerformance(DbContext db, ReportFiltersDto f, CancellationToken ct)
    {
        var q = db.Database.SqlQuery<PerformanceRow>($"SELECT * FROM platform.recruiter_performance")
            .Where(r => r.RequestedAt >= f.From && r.RequestedAt <= f.To);
        if (f.ClientId is { } client)
            q = q.Where(r => r.ClientId == client);
        if (f.RecruiterId is { } recruiter)
            q = q.Where(r => r.RecruiterId == recruiter);
        var rows = await q.ToListAsync(ct);

        var openings = await OpeningQueries.DisplayNames(db, rows.Select(r => r.JobOpeningId).Distinct().ToList(), ct);
        var clients = await ClientNames(db, rows.Select(r => r.ClientId), ct);
        var users = await UserNames.Of(db, rows.Select(r => (Guid?)r.RecruiterId), ct);
        var groups = rows.GroupBy(r => r.RecruiterId)
            .Select(g =>
            {
                var filled = g.Where(r => r.Status == OpeningStatus.Filled).ToList();
                return new RecruiterPerformanceGroupDto(g.Key, users.GetValueOrDefault(g.Key) ?? "", g.Count(), filled.Count,
                    ReportMath.Rate(filled.Count, g.Count()), ReportMath.Average(filled.Select(r => (decimal?)r.DaysToFill)),
                    g.OrderByDescending(r => r.RequestedAt).ThenBy(r => openings[r.JobOpeningId], StringComparer.CurrentCulture)
                        .Select(r => new RecruiterPerformanceRowDto(r.JobOpeningId, openings[r.JobOpeningId], clients[r.ClientId],
                            r.PositionName, r.IsLead, r.AssignedAt, r.Status, (int)r.CandidatesPresented, (int)r.CandidatesSuitable,
                            (int)r.CandidatesHired, ReportMath.Rate((int)r.CandidatesHired, (int)r.CandidatesPresented),
                            r.Status == OpeningStatus.Filled ? r.DaysToFill : null))
                        .ToList());
            })
            .OrderBy(g => g.RecruiterName, StringComparer.CurrentCulture).ToList();
        return new RecruiterPerformanceReportDto(f, groups);
    }

    // ---- 2 Resumen de reclutamiento ----

    public static async Task<SummaryReportDto> Summary(DbContext db, ReportFiltersDto f, CancellationToken ct)
    {
        var length = f.To.DayNumber - f.From.DayNumber + 1;
        var previousTo = f.From.AddDays(-1);
        var previousFrom = previousTo.AddDays(1 - length);
        var current = await SummaryRows(db, f.From, f.To, f.ClientId, f.RecruiterId, ct);
        var previous = await SummaryRows(db, previousFrom, previousTo, f.ClientId, f.RecruiterId, ct);
        var clients = await ClientNames(db, current.Select(r => r.ClientId), ct);
        return new SummaryReportDto(f, Metrics(current), previousFrom, previousTo, Metrics(previous),
            current.Select(r => new ClientSummaryDto(r.ClientId, clients[r.ClientId], Metrics([r])))
                .OrderBy(c => c.ClientName, StringComparer.CurrentCulture).ToList());
    }

    private static Task<List<SummaryRow>> SummaryRows(DbContext db, DateOnly from, DateOnly to, Guid? client, Guid? recruiter,
        CancellationToken ct) =>
        db.Database.SqlQuery<SummaryRow>($"SELECT * FROM platform.summary({from}, {to}, {client}::uuid, {recruiter}::uuid)").ToListAsync(ct);

    private static SummaryMetricsDto Metrics(IReadOnlyCollection<SummaryRow> rows)
    {
        int Sum(Func<SummaryRow, int> pick) => rows.Sum(pick);
        var filled = Sum(r => r.OpeningsFilled);
        var candidates = Sum(r => r.CandidatesTotal);
        var hired = Sum(r => r.CandidatesHired);
        return new SummaryMetricsDto(Sum(r => r.OpeningsTotal), Sum(r => r.OpeningsOpen), Sum(r => r.OpeningsInProgress), filled,
            Sum(r => r.OpeningsCancelled), Sum(r => r.OpeningsPaused), candidates, Sum(r => r.CandidatesSuitable), hired,
            ReportMath.Rate(hired, candidates), ReportMath.Mean(Sum(r => r.DaysToFillTotal), filled));
    }

    // ---- 3 Vacantes abiertas y envejecimiento ----

    public static async Task<OpenOpeningsReportDto> OpenOpenings(DbContext db, ReportFiltersDto f, IClock clock, CancellationToken ct)
    {
        var q = db.Database.SqlQuery<AgingRow>($"SELECT * FROM platform.open_openings_aging");
        if (f.ClientId is { } client)
            q = q.Where(r => r.ClientId == client);
        if (f.RecruiterId is { } recruiter)
            q = q.Where(r => db.Set<JobOpeningRecruiter>().Any(x => x.JobOpeningId == r.Id && x.UserId == recruiter));
        var rows = await q.ToListAsync(ct);

        var today = clock.Today();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(ClockExtensions.DefaultTimeZone);
        var openings = await OpeningQueries.DisplayNames(db, rows.Select(r => r.Id).ToList(), ct);
        var clients = await ClientNames(db, rows.Select(r => r.ClientId), ct);
        var users = await UserNames.Of(db, rows.Select(r => r.LeadRecruiterId), ct);
        var list = rows.Select(r =>
            {
                var idle = today.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(r.LastActivityAt, zone).DateTime).DayNumber;
                return new OpenOpeningDto(r.Id, openings[r.Id], clients[r.ClientId], r.PositionName, r.Priority, r.Status,
                    r.HeadcountRequested, (int)r.Hired, today.DayNumber - r.RequestedAt.DayNumber, (int)r.StageNew,
                    (int)r.StageContacted, (int)r.StageInterview, (int)r.StageSuitable, r.LastActivityAt, idle,
                    idle > StaleAfterDays, r.LeadRecruiterId, r.LeadRecruiterId is { } l ? users.GetValueOrDefault(l) : null);
            })
            .OrderByDescending(o => o.DaysOpen).ThenBy(o => o.OpeningName, StringComparer.CurrentCulture).ToList();
        return new OpenOpeningsReportDto(f, today, StaleAfterDays, list);
    }

    // ---- 4 Embudo por etapa ----

    public static async Task<StageFunnelReportDto> StageFunnel(DbContext db, ReportFiltersDto f, CancellationToken ct)
    {
        var (start, end) = Instants(f.From, f.To);
        var q = db.Database.SqlQuery<FunnelRow>($"SELECT * FROM platform.stage_funnel")
            .Where(r => r.ChangedAt >= start && r.ChangedAt < end);
        if (f.ClientId is { } client)
            q = q.Where(r => r.ClientId == client);
        if (f.RecruiterId is { } recruiter)
            q = q.Where(r => r.RecruiterId == recruiter);
        var rows = await q.ToListAsync(ct);

        var stages = FunnelStages.Select((stage, i) =>
            {
                // A candidate counts once per stage: their first entry into it within the range, and the move that followed.
                var entries = rows.Where(r => r.Stage == stage).GroupBy(r => r.CandidateId)
                    .Select(g => g.OrderBy(r => r.ChangedAt).First()).ToList();
                var lost = entries.Count(e => e.NextStage is CandidateStage.NotSuitable or CandidateStage.Discarded);
                if (stage == CandidateStage.Hired)
                    return new FunnelStageDto(stage, entries.Count, null, lost, null, null);
                var advanced = entries.Count(e => e.NextStage is { } next && Position(next) > i);
                var days = entries.Where(e => e.NextChangedAt is not null)
                    .Select(e => (decimal?)(decimal)(e.NextChangedAt!.Value - e.ChangedAt).TotalDays);
                return new FunnelStageDto(stage, entries.Count, advanced, lost, ReportMath.Rate(advanced, entries.Count),
                    ReportMath.Average(days));
            })
            .ToList();
        var losses = new[] { CandidateStage.NotSuitable, CandidateStage.Discarded }
            .Select(outcome =>
            {
                var moves = rows.Where(r => r.Stage == outcome).ToList();
                return new FunnelLossDto(outcome, moves.Count, moves
                    .GroupBy(m => string.IsNullOrWhiteSpace(m.Note) ? NoReason : m.Note.Trim(), StringComparer.CurrentCultureIgnoreCase)
                    .Select(g => new FunnelReasonDto(g.First().Note?.Trim() is { Length: > 0 } reason ? reason : NoReason, g.Count()))
                    .OrderByDescending(r => r.Count).ThenBy(r => r.Reason, StringComparer.CurrentCulture).ToList());
            })
            .ToList();
        return new StageFunnelReportDto(f, stages, losses);
    }

    public const string NoReason = "Sin motivo";

    private static int Position(CandidateStage stage) => FunnelStages.IndexOf(stage);

    private static int IndexOf(this IReadOnlyList<CandidateStage> list, CandidateStage stage)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == stage)
                return i;
        return -1;
    }

    // ---- 5 Fuentes de reclutamiento ----

    public static async Task<SourcesReportDto> Sources(DbContext db, ReportFiltersDto f, CancellationToken ct)
    {
        var q = db.Database.SqlQuery<SourceRow>($"SELECT * FROM platform.source_effectiveness")
            .Where(r => r.CapturedAt >= f.From && r.CapturedAt <= f.To);
        if (f.ClientId is { } client)
            q = q.Where(r => r.ClientId == client);
        if (f.RecruiterId is { } recruiter)
            q = q.Where(r => r.RecruiterId == recruiter);
        var rows = await q.ToListAsync(ct);

        var sources = Enum.GetValues<CandidateSource>().Select(s => (CandidateSource?)s).ToList();
        if (rows.Any(r => r.Source is null))
            sources.Add(null);
        var list = sources.Select(source =>
            {
                var of = rows.Where(r => r.Source == source).ToList();
                var hired = of.Where(r => r.IsHired).ToList();
                return new SourceRowDto(source, of.Count, of.Count(r => r.IsSuitable == true), hired.Count,
                    ReportMath.Rate(hired.Count, of.Count), ReportMath.Average(hired.Select(r => (decimal?)r.DaysToHire)));
            })
            .OrderByDescending(s => s.Conversion ?? -1).ThenByDescending(s => s.Candidates).ToList();
        return new SourcesReportDto(f, list);
    }

    // ---- 6 Vacantes por cliente ----

    public static async Task<ClientOpeningsReportDto> ClientOpenings(DbContext db, ReportFiltersDto f, CancellationToken ct)
    {
        var q = db.Database.SqlQuery<ClientOpeningRow>($"SELECT * FROM platform.client_openings")
            .Where(r => r.RequestedAt >= f.From && r.RequestedAt <= f.To);
        if (f.ClientId is { } client)
            q = q.Where(r => r.ClientId == client);
        if (f.RecruiterId is { } recruiter)
            q = q.Where(r => db.Set<JobOpeningRecruiter>().Any(x => x.JobOpeningId == r.JobOpeningId && x.UserId == recruiter));
        var rows = await q.ToListAsync(ct);

        var clients = await ClientNames(db, rows.Select(r => r.ClientId), ct);
        var list = rows.GroupBy(r => r.ClientId).Select(g => ClientRow(g.Key, clients[g.Key], g.ToList()))
            .OrderBy(r => r.ClientName, StringComparer.CurrentCulture).ToList();
        return new ClientOpeningsReportDto(f, list, ClientRow(null, "Total", rows));
    }

    private static ClientOpeningsRowDto ClientRow(Guid? clientId, string name, IReadOnlyCollection<ClientOpeningRow> rows)
    {
        var headcount = rows.Sum(r => r.HeadcountRequested);
        var hired = (int)rows.Sum(r => r.Hired);
        return new ClientOpeningsRowDto(clientId, name, rows.Count, headcount, hired, ReportMath.Rate(hired, headcount),
            rows.Count(r => r.Status == OpeningStatus.Cancelled),
            ReportMath.Average(rows.Where(r => r.Status == OpeningStatus.Filled).Select(r => (decimal?)r.DaysToFill)),
            (int)rows.Sum(r => r.CandidatesPresented));
    }

    // ---- shared ----

    private static async Task<Dictionary<Guid, string>> ClientNames(DbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var set = ids.Distinct().ToList();
        return await db.Set<Client>().Where(c => set.Contains(c.Uid)).ToDictionaryAsync(c => c.Uid, c => c.ShortName, ct);
    }

    /// <summary>The range as instants: 00:00 of <paramref name="from"/> to 00:00 after <paramref name="to"/>, America/Monterrey.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Instants(DateOnly from, DateOnly to)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(ClockExtensions.DefaultTimeZone);
        DateTimeOffset At(DateOnly d)
        {
            var local = d.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        }
        return (At(from), At(to.AddDays(1)));
    }

    /// <summary>The filters with their defaults (*Año en curso*) and display names; <c>null</c> when an id does not exist.</summary>
    public static async Task<ReportFiltersDto?> Filters(DbContext db, IClock clock, Guid? clientId, Guid? recruiterId, DateOnly? from,
        DateOnly? to, CancellationToken ct)
    {
        var today = clock.Today();
        var clientName = clientId is { } c ? await db.Set<Client>().Where(x => x.Uid == c).Select(x => x.ShortName).SingleOrDefaultAsync(ct) : null;
        var recruiterName = recruiterId is { } r ? await db.Set<User>().Where(x => x.Uid == r).Select(x => x.DisplayName).SingleOrDefaultAsync(ct) : null;
        if ((clientId is not null && clientName is null) || (recruiterId is not null && recruiterName is null))
            return null;
        return new ReportFiltersDto(clientId, clientName, recruiterId, recruiterName, from ?? new DateOnly(today.Year, 1, 1), to ?? today);
    }
}

/// <summary>
/// Rates and averages as shown, exported and stored in the files (see <c>ReportCell</c>): truncated, never rounded (PLAN D42, the
/// same «% conversión» as the Reclutadores cards — 2 of 3 is 66 %).
/// </summary>
public static class ReportMath
{
    /// <summary><paramref name="part"/> ÷ <paramref name="whole"/> truncated to whole percentage points (0.66), or <c>null</c> over zero.</summary>
    public static decimal? Rate(int part, int whole) => whole == 0 ? null : Cut((decimal)part / whole, 2);

    /// <summary>The mean truncated to 1 decimal, ignoring <c>null</c>s; <c>null</c> when there is nothing to average.</summary>
    public static decimal? Average(IEnumerable<decimal?> values)
    {
        var list = values.OfType<decimal>().ToList();
        return list.Count == 0 ? null : Cut(list.Average(), 1);
    }

    public static decimal? Mean(int total, int count) => count == 0 ? null : Cut((decimal)total / count, 1);

    public static decimal Cut(decimal value, int decimals)
    {
        var factor = decimals == 1 ? 10m : 100m;
        return Math.Truncate(value * factor) / factor;
    }
}
