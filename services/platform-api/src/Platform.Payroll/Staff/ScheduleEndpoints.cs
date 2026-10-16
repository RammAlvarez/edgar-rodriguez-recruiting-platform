using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Groups;

namespace Platform.Payroll.Staff;

/// <summary>«Horario»: recurring on weekdays (1 = lunes … 7 = domingo) or a one-off job on <see cref="OneOffDate"/>.</summary>
public sealed record WorkScheduleRequest(Guid ClientSiteId, IReadOnlyList<short> Weekdays, DateOnly? OneOffDate, TimeOnly StartTime,
    TimeOnly EndTime, DateOnly? ValidFrom, DateOnly? ValidTo, string? JobDescription);

public sealed record WorkScheduleDto(Guid Id, Guid ClientSiteId, string SiteName, IReadOnlyList<short> Weekdays, DateOnly? OneOffDate,
    TimeOnly StartTime, TimeOnly EndTime, DateOnly ValidFrom, DateOnly? ValidTo, string? JobDescription);

/// <summary>One expected employee at a site on a day, with the attendance already recorded for that day (if any).</summary>
public sealed record CalendarEntryDto(Guid EmployeeId, string FullName, string PositionName, TimeOnly StartTime, TimeOnly EndTime,
    string? JobDescription, AttendanceStatus? Attendance);

public sealed record CalendarSiteDto(Guid SiteId, string SiteName, Guid ClientId, string ClientName, IReadOnlyList<CalendarEntryDto> Entries);

public sealed record CalendarDayDto(DateOnly Date, IReadOnlyList<CalendarSiteDto> Sites);

internal sealed class WorkScheduleRequestValidator : AbstractValidator<WorkScheduleRequest>
{
    public WorkScheduleRequestValidator()
    {
        RuleFor(r => r.Weekdays).Must(w => w.All(d => d is >= 1 and <= 7)).WithMessage("Días de la semana inválidos.");
        RuleFor(r => r.Weekdays).Must((r, w) => (w.Count > 0) != (r.OneOffDate is not null))
            .WithMessage("Elige los días de la semana o una fecha única, no ambos.");
        RuleFor(r => r.EndTime).NotEqual(r => r.StartTime).WithMessage("La hora de salida es igual a la de entrada.");
        RuleFor(r => r.ValidTo).Must((r, to) => to is not { } t || r.ValidFrom is not { } f || t >= f)
            .WithMessage("La vigencia termina antes de empezar.");
        RuleFor(r => r.JobDescription).OptionalText(120);
    }
}

/// <summary>
/// Calendario de servicios (SPEC §5.2): work schedules are edited on the employee (grant <c>payroll_staff</c>); the calendar
/// expands them by date with the attendance of each day (grant <c>payroll_attendance</c>, the supervisors' module — its day
/// view becomes the entry to attendance capture in S8).
/// </summary>
public static class ScheduleEndpoints
{
    public const string Tag = "schedules";

    public static readonly AccessRule CalendarAccess = AccessRule.Administrator(PermissionModule.PayrollAttendance);

    public const string SiteNotOfClient = "Elige un sitio del cliente del empleado.";
    public const string ScheduleNotFound = "El horario no existe.";
    public const string RangeTooLong = "Consulta como máximo 42 días.";

    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/employees/{id:guid}/schedules").WithTags("Employees");

        g.MapGet("", async (Guid id, DbContext db, RequestContext ctx, CancellationToken ct) =>
                await Employee(db, ctx, id, ct) is null ? ApiResults.NotFound(EmployeeEndpoints.NotFound) : TypedResults.Ok(await List(db, id, ct)))
            .WithName("ListWorkSchedules").Produces<List<WorkScheduleDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAccess(EmployeeEndpoints.Access);

        g.MapPost("", async (Guid id, WorkScheduleRequest r, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Employee(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(EmployeeEndpoints.NotFound);
                if (await SiteProblem(db, e, r.ClientSiteId, ct) is { } bad)
                    return bad;
                var schedule = new WorkSchedule { EmployeeId = id };
                Apply(schedule, r, clock.Today());
                db.Add(schedule);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await List(db, id, ct));
            })
            .WithName("CreateWorkSchedule").Mutation().RequireAccess(EmployeeEndpoints.Access);

        g.MapPut("/{scheduleId:guid}", async (Guid id, Guid scheduleId, WorkScheduleRequest r, DbContext db, RequestContext ctx, IClock clock,
                CancellationToken ct) =>
            {
                if (await Employee(db, ctx, id, ct) is not { } e)
                    return ApiResults.NotFound(EmployeeEndpoints.NotFound);
                if (await db.Set<WorkSchedule>().SingleOrDefaultAsync(s => s.Uid == scheduleId && s.EmployeeId == id, ct) is not { } schedule)
                    return ApiResults.NotFound(ScheduleNotFound);
                if (await SiteProblem(db, e, r.ClientSiteId, ct) is { } bad)
                    return bad;
                Apply(schedule, r, schedule.ValidFrom);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await List(db, id, ct));
            })
            .WithName("UpdateWorkSchedule").Mutation().RequireAccess(EmployeeEndpoints.Access);

        // Removing a schedule that already ran ends it yesterday, so the calendar's past stays as it was.
        g.MapDelete("/{scheduleId:guid}", async (Guid id, Guid scheduleId, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                if (await Employee(db, ctx, id, ct) is null)
                    return ApiResults.NotFound(EmployeeEndpoints.NotFound);
                if (await db.Set<WorkSchedule>().SingleOrDefaultAsync(s => s.Uid == scheduleId && s.EmployeeId == id, ct) is not { } schedule)
                    return ApiResults.NotFound(ScheduleNotFound);
                var today = clock.Today();
                if (schedule.ValidFrom >= today || (schedule.OneOffDate is { } d && d >= today))
                    db.Remove(schedule);
                else
                    schedule.ValidTo = today.AddDays(-1);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(await List(db, id, ct));
            })
            .WithName("DeleteWorkSchedule").Produces<List<WorkScheduleDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(Tag).RequireAccess(EmployeeEndpoints.Access);

        v1.MapGet("/calendar", async (DateOnly from, DateOnly to, Guid? clientId, DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                if (to < from || to.DayNumber - from.DayNumber > 41)
                    return ApiResults.Validation(new Dictionary<string, string[]> { ["to"] = [RangeTooLong] });
                return TypedResults.Ok(await Calendar(db, PayrollScope.Groups(db, ctx, PermissionModule.PayrollAttendance), from, to, clientId, ct));
            })
            .WithName("GetCalendar").WithTags("Calendar").Produces<List<CalendarDayDto>>().ProducesValidationProblem()
            .RequireAccess(CalendarAccess);
        return v1;
    }

    private static RouteHandlerBuilder Mutation(this RouteHandlerBuilder route) =>
        route.Validate<WorkScheduleRequest>().Produces<List<WorkScheduleDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(Tag);

    private static Task<Employee?> Employee(DbContext db, RequestContext ctx, Guid id, CancellationToken ct) =>
        PayrollScope.Employees(db, ctx, PermissionModule.PayrollStaff).AsNoTracking().SingleOrDefaultAsync(e => e.Uid == id, ct);

    private static async Task<IResult?> SiteProblem(DbContext db, Employee e, Guid siteId, CancellationToken ct)
    {
        var clientId = await db.Set<PayrollGroup>().Where(g => g.Uid == e.PayrollGroupId).Select(g => g.ClientId).SingleAsync(ct);
        return await db.Set<ClientSite>().AnyAsync(s => s.Uid == siteId && s.ClientId == clientId && s.IsActive, ct)
            ? null
            : ApiResults.Validation(new Dictionary<string, string[]> { ["clientSiteId"] = [SiteNotOfClient] });
    }

    private static void Apply(WorkSchedule s, WorkScheduleRequest r, DateOnly defaultFrom)
    {
        s.ClientSiteId = r.ClientSiteId;
        s.Weekdays = [.. r.Weekdays.Distinct().Order()];
        s.OneOffDate = r.OneOffDate;
        s.StartTime = r.StartTime;
        s.EndTime = r.EndTime;
        s.ValidFrom = r.OneOffDate ?? r.ValidFrom ?? defaultFrom;
        s.ValidTo = r.OneOffDate ?? r.ValidTo;
        s.JobDescription = r.JobDescription.Clean();
    }

    private static async Task<List<WorkScheduleDto>> List(DbContext db, Guid employeeId, CancellationToken ct) =>
        (await db.Set<WorkSchedule>().Where(s => s.EmployeeId == employeeId)
            .Join(db.Set<ClientSite>(), s => s.ClientSiteId, c => c.Uid, (s, c) => new { s, c.Name })
            .AsNoTracking().ToListAsync(ct))
        .OrderBy(x => x.s.OneOffDate.HasValue).ThenByDescending(x => x.s.ValidFrom)
        .Select(x => new WorkScheduleDto(x.s.Uid, x.s.ClientSiteId, x.Name, x.s.Weekdays, x.s.OneOffDate, x.s.StartTime, x.s.EndTime,
            x.s.ValidFrom, x.s.ValidTo, x.s.JobDescription))
        .ToList();

    /// <summary>ISO weekday: 1 = lunes … 7 = domingo.</summary>
    public static short IsoWeekday(DateOnly d) => (short)(d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek);

    /// <summary>
    /// The schedules active in the range expanded by date, for employees hired and not dado de baja that day, with that day's
    /// attendance (a vacation or an incapacidad shows on the expected employee).
    /// </summary>
    public static async Task<List<CalendarDayDto>> Calendar(DbContext db, IQueryable<PayrollGroup> groups, DateOnly from, DateOnly to,
        Guid? clientId, CancellationToken ct)
    {
        if (clientId is { } c)
            groups = groups.Where(g => g.ClientId == c);
        var rows = await db.Set<WorkSchedule>()
            .Where(s => s.ValidFrom <= to && (s.ValidTo == null || s.ValidTo >= from))
            .Join(db.Set<Employee>(), s => s.EmployeeId, e => e.Uid, (s, e) => new { s, e })
            .Where(x => groups.Any(g => g.Uid == x.e.PayrollGroupId) && x.e.HiredAt <= to && (x.e.TerminatedAt == null || x.e.TerminatedAt > from))
            .Join(db.Set<Person>(), x => x.e.PersonId, p => p.Uid, (x, p) => new { x.s, x.e, p.FullName })
            .Join(db.Set<Position>(), x => x.e.PositionId, p => p.Uid, (x, p) => new { x.s, x.e, x.FullName, Position = p.Name })
            .Join(db.Set<ClientSite>(), x => x.s.ClientSiteId, site => site.Uid, (x, site) => new { x.s, x.e, x.FullName, x.Position, Site = site })
            .Join(db.Set<Client>(), x => x.Site.ClientId, cl => cl.Uid, (x, cl) => new { x.s, x.e, x.FullName, x.Position, x.Site, Client = cl.ShortName })
            .AsNoTracking().ToListAsync(ct);
        var employeeIds = rows.Select(r => r.e.Uid).Distinct().ToList();
        var attendance = await db.Set<AttendanceRecord>().AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to)
            .ToDictionaryAsync(a => (a.EmployeeId, a.WorkDate), a => a.Status, ct);

        var days = new List<CalendarDayDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var weekday = IsoWeekday(d);
            var today = rows.Where(r => r.e.HiredAt <= d && (r.e.TerminatedAt == null || r.e.TerminatedAt > d)
                && r.s.ValidFrom <= d && (r.s.ValidTo == null || r.s.ValidTo >= d)
                && (r.s.OneOffDate == d || (r.s.OneOffDate == null && r.s.Weekdays.Contains(weekday))));
            var date = d;
            days.Add(new CalendarDayDto(d, today.GroupBy(r => r.Site.Uid)
                .Select(site => new CalendarSiteDto(site.Key, site.First().Site.Name, site.First().Site.ClientId, site.First().Client,
                    site.OrderBy(r => r.s.StartTime).ThenBy(r => r.FullName, StringComparer.Ordinal)
                        .Select(r => new CalendarEntryDto(r.e.Uid, r.FullName, r.Position, r.s.StartTime, r.s.EndTime, r.s.JobDescription,
                            attendance.TryGetValue((r.e.Uid, date), out var status) ? status : null))
                        .ToList()))
                .OrderBy(s => s.ClientName, StringComparer.Ordinal).ThenBy(s => s.SiteName, StringComparer.Ordinal).ToList()));
        }
        return days;
    }
}
