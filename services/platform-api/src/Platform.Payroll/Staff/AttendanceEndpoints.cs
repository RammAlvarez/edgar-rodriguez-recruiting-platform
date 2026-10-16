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
using Platform.Core.Users;
using Platform.Payroll.Groups;

namespace Platform.Payroll.Staff;

public sealed record AttendanceSiteDto(Guid SiteId, string SiteName, Guid ClientId, string ClientName, int Expected, int Captured);

public sealed record AttendanceRecordDto(Guid Id, DateOnly WorkDate, AttendanceStatus Status, decimal? HoursWorked, decimal OvertimeHours,
    Guid? ClientSiteId, string? SiteName, string? Note, bool IsLocked, bool IsAutoAbsent, bool FromStatusPeriod);

/// <summary>
/// One row of the capture roster: an employee expected at the site that day (schedule) or with a record there.
/// <see cref="Locked"/> = the day's lock moment passed for the employee's group; <see cref="StatusPeriod"/> names a vacation,
/// incapacidad or suspension that owns the day (read-only here).
/// </summary>
public sealed record AttendanceEntryDto(Guid EmployeeId, string FullName, string PositionName, decimal ShiftHours, TimeOnly? StartTime,
    TimeOnly? EndTime, bool Scheduled, AttendanceRecordDto? Record, bool Locked, string? StatusPeriod);

public sealed record EmployeePickDto(Guid EmployeeId, string FullName, string PositionName);

/// <summary>
/// The capture screen for one site and date (SPEC §5.3). <see cref="Holiday"/> names the statutory holiday (D51: a holiday
/// is captured as Festivo or Festivo laborado); <see cref="CanUnlock"/> = the caller may change locked days (D83).
/// </summary>
public sealed record AttendanceDayDto(DateOnly Date, string? Holiday, Guid SiteId, string SiteName, Guid ClientId, string ClientName,
    bool CanUnlock, IReadOnlyList<AttendanceEntryDto> Entries, IReadOnlyList<EmployeePickDto> Others);

public sealed record AttendanceEntryRequest(Guid EmployeeId, AttendanceStatus Status, decimal? HoursWorked, decimal? OvertimeHours, string? Note);

/// <summary>«Guardar» / «Todos presentes»: every entry is checked first; nothing is saved when one fails.</summary>
public sealed record SaveAttendanceRequest(Guid SiteId, DateOnly Date, IReadOnlyList<AttendanceEntryRequest> Entries);

/// <summary>«Desbloquear»: the new value of a locked day and why (DATA_MODEL §5.8).</summary>
public sealed record UnlockAttendanceRequest(Guid EmployeeId, DateOnly Date, Guid? SiteId, AttendanceStatus Status, decimal? HoursWorked,
    decimal? OvertimeHours, string? Note, string Reason);

/// <summary>
/// Asistencia (SPEC §5.3, PLAN D83): capture by site and date for supervisors (grant <c>payroll_attendance</c>, usually limited
/// to their client), the lock rule applied on every write, and the unlock — a change with a reason — reserved to an
/// Administrador whose Asistencia grant covers every client.
/// </summary>
public static class AttendanceEndpoints
{
    public const string Tag = "attendance";

    public static readonly AccessRule Access = AccessRule.Administrator(PermissionModule.PayrollAttendance);

    public const string SiteNotFound = "El sitio no existe o no es de tus clientes.";

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/attendance").WithTags("Attendance");

        g.MapGet("/sites", async (DateOnly date, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var groups = PayrollScope.Groups(db, ctx, PermissionModule.PayrollAttendance);
                var day = (await ScheduleEndpoints.Calendar(db, groups, date, date, null, ct)).Single();
                return TypedResults.Ok(day.Sites.Select(s => new AttendanceSiteDto(s.SiteId, s.SiteName, s.ClientId, s.ClientName,
                    s.Entries.Count, s.Entries.Count(e => e.Attendance is not null))).ToList());
            })
            .WithName("ListAttendanceSites").Produces<List<AttendanceSiteDto>>().RequireAccess(Access);

        g.MapGet("/day", async (Guid siteId, DateOnly date, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
                await Day(db, ctx, siteId, date, clock.UtcNow, ct) is { } day ? TypedResults.Ok(day) : ApiResults.NotFound(SiteNotFound))
            .WithName("GetAttendanceDay").Produces<AttendanceDayDto>().ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(Access);

        g.MapPut("/day", async (SaveAttendanceRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var site = await Site(db, ctx, r.SiteId, ct);
                if (site is null)
                    return ApiResults.NotFound(SiteNotFound);
                var now = clock.UtcNow;
                var errors = new Dictionary<string, string[]>();
                var writes = new List<(Employee E, PayrollGroup G, AttendanceEntryRequest R, decimal? Hours, decimal Overtime)>();
                for (var i = 0; i < r.Entries.Count; i++)
                {
                    var entry = r.Entries[i];
                    var found = await EmployeeAt(db, ctx, entry.EmployeeId, site.ClientId, ct);
                    if (found is not { } x)
                        return ApiResults.NotFound(EmployeeEndpoints.NotFound);
                    if (await Problem(db, x.E, x.G, x.Name, r.Date, now, ct) is { } conflict)
                        return conflict;
                    if (AttendanceRules.IsLocked(x.G, r.Date, now))
                        return ApiResults.Conflict($"{x.Name}: {AttendanceRules.Locked}");
                    var (hours, overtime, field, problem) = AttendanceRules.Normalize(x.G, entry.Status, entry.HoursWorked, entry.OvertimeHours);
                    if (problem is not null)
                        errors[$"entries.{i}.{field}"] = [$"{x.Name}: {problem}"];
                    else if (entry.Note.Clean() is { Length: > 200 })
                        errors[$"entries.{i}.note"] = ["La nota admite hasta 200 caracteres."];
                    else
                        writes.Add((x.E, x.G, entry, hours, overtime));
                }
                if (errors.Count > 0)
                    return ApiResults.Validation(errors);
                foreach (var w in writes)
                {
                    var day = await db.Set<AttendanceRecord>().SingleOrDefaultAsync(a => a.EmployeeId == w.E.Uid && a.WorkDate == r.Date, ct);
                    if (day is null)
                        db.Add(day = new AttendanceRecord { EmployeeId = w.E.Uid, WorkDate = r.Date });
                    Apply(day, w.R.Status, w.Hours, w.Overtime, site.Uid, w.R.Note, ctx.RequiredPrincipal.UserId, now);
                }
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await Day(db, ctx, r.SiteId, r.Date, now, ct));
            })
            .WithName("SaveAttendanceDay").Produces<AttendanceDayDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .Invalidates(Tag, ScheduleEndpoints.Tag, EmployeeEndpoints.Tag).RequireAccess(Access);

        g.MapPost("/unlock", async (UnlockAttendanceRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                // «a menos que yo le dé permiso»: a supervisor's grant is limited to their client, so it cannot unlock (D83).
                if (ctx.RequiredPrincipal.ClientScope(PermissionModule.PayrollAttendance) is not null)
                    return ApiResults.Forbidden(AttendanceRules.UnlockNeedsAllClients);
                if (r.Reason.Clean() is not { } reason)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["reason"] = [AttendanceRules.ReasonRequired] });
                var found = await EmployeeAt(db, ctx, r.EmployeeId, null, ct);
                if (found is not { } x)
                    return ApiResults.NotFound(EmployeeEndpoints.NotFound);
                var now = clock.UtcNow;
                if (await Problem(db, x.E, x.G, x.Name, r.Date, now, ct) is { } conflict)
                    return conflict;
                if (!AttendanceRules.IsLocked(x.G, r.Date, now))
                    return ApiResults.Conflict(AttendanceRules.NotLocked);
                var (hours, overtime, field, problem) = AttendanceRules.Normalize(x.G, r.Status, r.HoursWorked, r.OvertimeHours);
                if (problem is not null)
                    return ApiResults.Validation(new Dictionary<string, string[]> { [field!] = [problem] });
                if (r.SiteId is { } s && !await db.Set<ClientSite>().AnyAsync(c => c.Uid == s && c.ClientId == x.G.ClientId, ct))
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["siteId"] = [ScheduleEndpoints.SiteNotOfClient] });

                var day = await db.Set<AttendanceRecord>().SingleOrDefaultAsync(a => a.EmployeeId == x.E.Uid && a.WorkDate == r.Date, ct);
                var previous = day is null ? null : AttendanceRules.Snapshot(day);
                if (day is null)
                    db.Add(day = new AttendanceRecord { EmployeeId = x.E.Uid, WorkDate = r.Date });
                Apply(day, r.Status, hours, overtime, r.SiteId ?? day.ClientSiteId, r.Note, ctx.RequiredPrincipal.UserId, now);
                // The day stays locked: another change needs another reason.
                day.IsLocked = true;
                db.Add(new AttendanceUnlock
                {
                    AttendanceRecordId = day.Uid,
                    UnlockedBy = ctx.RequiredPrincipal.UserId,
                    UnlockedAt = now,
                    Reason = reason,
                    PreviousValue = previous,
                    NewValue = AttendanceRules.Snapshot(day),
                });
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await RecordDto(db, day, ct));
            })
            .WithName("UnlockAttendanceDay").Produces<AttendanceRecordDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).Invalidates(Tag, ScheduleEndpoints.Tag, EmployeeEndpoints.Tag).RequireAccess(Access);

        // The employee detail's Asistencia tab (Personal).
        v1.MapGet("/employees/{id:guid}/attendance", async (Guid id, DateOnly from, DateOnly to, DbContext db, RequestContext ctx,
                CancellationToken ct) =>
            {
                if (to < from || to.DayNumber - from.DayNumber > 62)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["to"] = ["Consulta como máximo 62 días."] });
                if (!await PayrollScope.Employees(db, ctx, PermissionModule.PayrollStaff).AnyAsync(e => e.Uid == id, ct))
                    return ApiResults.NotFound(EmployeeEndpoints.NotFound);
                var rows = await db.Set<AttendanceRecord>().AsNoTracking().Where(a => a.EmployeeId == id && a.WorkDate >= from && a.WorkDate <= to)
                    .GroupJoin(db.Set<ClientSite>(), a => a.ClientSiteId, s => s.Uid, (a, s) => new { a, s })
                    .SelectMany(x => x.s.DefaultIfEmpty(), (x, s) => new { x.a, Site = s == null ? null : s.Name })
                    .OrderByDescending(x => x.a.WorkDate).ToListAsync(ct);
                return TypedResults.Ok(rows.Select(x => Dto(x.a, x.Site)).ToList());
            })
            .WithName("ListEmployeeAttendance").WithTags("Employees").Produces<List<AttendanceRecordDto>>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).RequireAccess(EmployeeEndpoints.Access);
        return v1;
    }

    private static void Apply(AttendanceRecord day, AttendanceStatus status, decimal? hours, decimal overtime, Guid? siteId, string? note,
        Guid userId, DateTimeOffset now)
    {
        day.Status = status;
        day.HoursWorked = hours;
        day.OvertimeHours = overtime;
        day.ClientSiteId = siteId;
        day.Note = note.Clean();
        day.IsAutoAbsent = false;
        day.RecordedBy = userId;
        day.RecordedAt = now;
    }

    private static AttendanceRecordDto Dto(AttendanceRecord a, string? site) =>
        new(a.Uid, a.WorkDate, a.Status, a.HoursWorked, a.OvertimeHours, a.ClientSiteId, site, a.Note, a.IsLocked, a.IsAutoAbsent,
            a.StatusPeriodId is not null);

    private static async Task<AttendanceRecordDto> RecordDto(DbContext db, AttendanceRecord a, CancellationToken ct) =>
        Dto(a, a.ClientSiteId is { } s ? await db.Set<ClientSite>().Where(c => c.Uid == s).Select(c => c.Name).SingleOrDefaultAsync(ct) : null);

    private static async Task<ClientSite?> Site(DbContext db, RequestContext ctx, Guid siteId, CancellationToken ct)
    {
        var groups = PayrollScope.Groups(db, ctx, PermissionModule.PayrollAttendance);
        return await db.Set<ClientSite>().AsNoTracking()
            .SingleOrDefaultAsync(s => s.Uid == siteId && groups.Any(g => g.ClientId == s.ClientId), ct);
    }

    /// <summary>The employee (in the caller's Asistencia scope, of the site's client when given) with their group and name.</summary>
    private static async Task<(Employee E, PayrollGroup G, string Name)?> EmployeeAt(DbContext db, RequestContext ctx, Guid employeeId,
        Guid? clientId, CancellationToken ct)
    {
        var x = await PayrollScope.Employees(db, ctx, PermissionModule.PayrollAttendance).AsNoTracking().Where(e => e.Uid == employeeId)
            .Join(db.Set<PayrollGroup>(), e => e.PayrollGroupId, g => g.Uid, (e, g) => new { e, g })
            .Join(db.Set<Person>(), x => x.e.PersonId, p => p.Uid, (x, p) => new { x.e, x.g, p.FullName })
            .SingleOrDefaultAsync(ct);
        if (x is null || (clientId is { } c && x.g.ClientId != c))
            return null;
        return (x.e, x.g, x.FullName);
    }

    /// <summary>Not a future day, the employee worked in the group that day, and no status period owns it.</summary>
    private static async Task<IResult?> Problem(DbContext db, Employee e, PayrollGroup g, string name, DateOnly date, DateTimeOffset now,
        CancellationToken ct)
    {
        if (date > AttendanceRules.Today(g, now))
            return ApiResults.Conflict(AttendanceRules.Future);
        if (date < e.HiredAt || (e.TerminatedAt is { } t && date >= t))
            return ApiResults.Conflict($"{name}: {AttendanceRules.NotEmployed(date)}");
        var period = await db.Set<EmployeeStatusPeriod>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmployeeId == e.Uid && p.StartDate <= date && (p.EndDate == null || p.EndDate >= date), ct);
        return period is null ? null : ApiResults.Conflict($"{name}: {AttendanceRules.StatusPeriodDay(period)}");
    }

    private static async Task<AttendanceDayDto?> Day(DbContext db, RequestContext ctx, Guid siteId, DateOnly date, DateTimeOffset now,
        CancellationToken ct)
    {
        var site = await Site(db, ctx, siteId, ct);
        if (site is null)
            return null;
        var client = await db.Set<Client>().Where(c => c.Uid == site.ClientId).Select(c => c.ShortName).SingleAsync(ct);
        var groups = PayrollScope.Groups(db, ctx, PermissionModule.PayrollAttendance);
        var expected = (await ScheduleEndpoints.Calendar(db, groups, date, date, site.ClientId, ct)).Single()
            .Sites.FirstOrDefault(s => s.SiteId == siteId)?.Entries ?? [];
        var recordedHere = await db.Set<AttendanceRecord>().AsNoTracking()
            .Where(a => a.WorkDate == date && a.ClientSiteId == siteId).Select(a => a.EmployeeId).ToListAsync(ct);
        var ids = expected.Select(x => x.EmployeeId).Union(recordedHere).ToList();

        var employees = await PayrollScope.Employees(db, ctx, PermissionModule.PayrollAttendance).AsNoTracking()
            .Where(e => groups.Any(g => g.Uid == e.PayrollGroupId && g.ClientId == site.ClientId))
            .Join(db.Set<PayrollGroup>(), e => e.PayrollGroupId, g => g.Uid, (e, g) => new { e, g })
            .Join(db.Set<Person>(), x => x.e.PersonId, p => p.Uid, (x, p) => new { x.e, x.g, p.FullName })
            .Join(db.Set<Position>(), x => x.e.PositionId, p => p.Uid, (x, p) => new { x.e, x.g, x.FullName, Position = p.Name })
            .ToListAsync(ct);
        var records = await db.Set<AttendanceRecord>().AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.WorkDate == date)
            .ToDictionaryAsync(a => a.EmployeeId, ct);
        var siteNames = await db.Set<ClientSite>().Where(s => s.ClientId == site.ClientId).ToDictionaryAsync(s => s.Uid, s => s.Name, ct);
        var periods = await db.Set<EmployeeStatusPeriod>().AsNoTracking()
            .Where(p => ids.Contains(p.EmployeeId) && p.StartDate <= date && (p.EndDate == null || p.EndDate >= date)).ToListAsync(ct);
        var holiday = await db.Set<Holiday>().Where(h => h.Date == date).Select(h => h.NameEs).FirstOrDefaultAsync(ct);

        var entries = employees.Where(x => ids.Contains(x.e.Uid))
            .Select(x =>
            {
                var schedule = expected.FirstOrDefault(s => s.EmployeeId == x.e.Uid);
                var record = records.GetValueOrDefault(x.e.Uid);
                var period = periods.FirstOrDefault(p => p.EmployeeId == x.e.Uid);
                return new AttendanceEntryDto(x.e.Uid, x.FullName, x.Position, x.g.ShiftHours, schedule?.StartTime, schedule?.EndTime,
                    schedule is not null, record is null ? null : Dto(record, record.ClientSiteId is { } s ? siteNames.GetValueOrDefault(s) : null),
                    AttendanceRules.IsLocked(x.g, date, now), period is null ? null : StaffRules.Label(period.Status));
            })
            .OrderBy(x => x.StartTime).ThenBy(x => x.FullName, StringComparer.Ordinal).ToList();
        var others = employees.Where(x => !ids.Contains(x.e.Uid) && x.e.HiredAt <= date && (x.e.TerminatedAt == null || x.e.TerminatedAt > date))
            .OrderBy(x => x.FullName, StringComparer.Ordinal).Select(x => new EmployeePickDto(x.e.Uid, x.FullName, x.Position)).ToList();
        return new AttendanceDayDto(date, holiday, site.Uid, site.Name, site.ClientId, client,
            ctx.RequiredPrincipal.ClientScope(PermissionModule.PayrollAttendance) is null, entries, others);
    }
}
