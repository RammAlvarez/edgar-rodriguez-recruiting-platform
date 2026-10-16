using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;

namespace Platform.Recruiting.Openings;

/// <summary>Server rules of a vacante: references, the derived status (PLAN D70), líder / apoyo (D71) and Actividad entries.</summary>
public static class OpeningRules
{
    public static readonly OpeningStatus[] Active = [OpeningStatus.Open, OpeningStatus.InProgress];
    private static readonly OpeningStatus[] Derived = [OpeningStatus.Open, OpeningStatus.InProgress, OpeningStatus.Filled];

    /// <summary>
    /// Sets <c>open</c> / <c>in_progress</c> / <c>filled</c> from the saved candidates. Call after the candidate write is saved,
    /// in the same transaction, and save again. Paused and cancelled vacantes keep their status.
    /// </summary>
    public static async Task RecomputeAsync(DbContext db, JobOpening o, RequestContext ctx, IClock clock, CancellationToken ct)
    {
        if (!Derived.Contains(o.Status))
            return;
        var counts = await db.Set<Candidate>().Where(c => c.JobOpeningId == o.Uid)
            .GroupBy(_ => 1).Select(g => new { Total = g.Count(), Hired = g.Count(c => c.IsHired) }).SingleOrDefaultAsync(ct);
        SetDerived(db, o, counts?.Total ?? 0, counts?.Hired ?? 0, ctx, clock);
    }

    public static void SetDerived(DbContext db, JobOpening o, int candidates, int hired, RequestContext ctx, IClock clock)
    {
        var target = hired >= o.HeadcountRequested ? OpeningStatus.Filled
            : candidates > 0 ? OpeningStatus.InProgress
            : OpeningStatus.Open;
        if (target == o.Status)
            return;
        AddEvent(db, ctx, clock, o, OpeningEventKind.StatusChanged, from: o.Status, to: target);
        o.Status = target;
        o.FilledAt = target == OpeningStatus.Filled ? clock.Today() : null;
    }

    public static void AddEvent(DbContext db, RequestContext ctx, IClock clock, JobOpening o, OpeningEventKind kind,
        OpeningStatus? from = null, OpeningStatus? to = null, string? reason = null, Guid? recruiter = null) =>
        db.Add(new JobOpeningEvent
        {
            JobOpeningId = o.Uid,
            Kind = kind,
            FromStatus = from,
            ToStatus = to,
            Reason = reason,
            RecruiterUserId = recruiter,
            UserId = ctx.Principal?.UserId,
            ActingForUserId = ctx.ActingFor?.UserId,
            OccurredAt = clock.UtcNow,
        });

    /// <summary>Client, zona, nivel de estudios, plantilla (candidate scope), bancos and líder must exist; 400 on the field.</summary>
    public static async Task<IResult?> ReferenceProblems(DbContext db, OpeningRequest r, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!await db.Set<Client>().AnyAsync(c => c.Uid == r.ClientId && c.IsActive, ct))
            errors["clientId"] = ["El cliente no existe o está inactivo."];
        if (r.ZoneId is { } zone && !await db.Set<Zone>().AnyAsync(z => z.Uid == zone, ct))
            errors["zoneId"] = ["La zona no existe."];
        if (r.EducationLevelId is { } level && !await db.Set<EducationLevel>().AnyAsync(e => e.Uid == level, ct))
            errors["educationLevelId"] = ["El nivel de estudios no existe."];
        if (r.DocumentTemplateId is { } template
            && !await db.Set<DocumentTemplate>().AnyAsync(t => t.Uid == template && t.Scope == DocumentTemplateScope.Candidate, ct))
            errors["documentTemplateId"] = ["Elige una plantilla de documentos para candidatos."];
        var banks = (r.PaymentBankIds ?? []).Distinct().ToList();
        if (banks.Count > 0 && await db.Set<Bank>().CountAsync(b => banks.Contains(b.Uid), ct) != banks.Count)
            errors["paymentBankIds"] = ["Uno de los bancos no existe."];
        if (r.LeadRecruiterId is { } lead && !await IsActiveRecruiter(db, lead, ct))
            errors["leadRecruiterId"] = [OpeningMessages.NotARecruiter];
        return errors.Count == 0 ? null : ApiResults.Validation(errors);
    }

    public static Task<bool> IsActiveRecruiter(DbContext db, Guid userId, CancellationToken ct) =>
        db.Set<User>().AnyAsync(u => u.Uid == userId && u.Role == UserRole.Recruiter && u.IsActive, ct);

    public static void Apply(JobOpening o, OpeningRequest r)
    {
        o.ClientId = r.ClientId;
        o.ZoneId = r.ZoneId;
        o.PositionName = PositionName(r.PositionName);
        o.HeadcountRequested = r.HeadcountRequested;
        o.MonthlySalary = r.MonthlySalary;
        o.Benefits = (r.Benefits ?? []).Select(b => b.Trim()).Where(b => b.Length > 0).Distinct().ToList();
        o.Schedule = r.Schedule.Clean();
        o.PaymentBankIds = (r.PaymentBankIds ?? []).Distinct().ToList();
        o.EducationLevelId = r.EducationLevelId;
        o.YearsExperience = r.YearsExperience;
        o.RequiredExperience = r.RequiredExperience.Clean();
        o.GenderRequirement = r.GenderRequirement;
        o.AgeRange = r.AgeRange.Clean();
        o.Priority = r.Priority;
        o.DocumentTemplateId = r.DocumentTemplateId;
        o.VisibleToClient = r.VisibleToClient;
        o.Notes = r.Notes.Clean();
    }

    /// <summary>Puestos are written as in the client's files: uppercase, single spaces.</summary>
    public static string PositionName(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>
    /// Adds the recruiter as apoyo when not assigned yet (Actividad «Asignada a … como apoyo»). <paramref name="log"/> is false
    /// when the row is created to make her líder: that entry alone says «como líder».
    /// </summary>
    public static JobOpeningRecruiter Assign(DbContext db, RequestContext ctx, IClock clock, JobOpening o, Guid userId, bool log = true)
    {
        if (o.Recruiters.FirstOrDefault(r => r.UserId == userId) is { } existing)
            return existing;
        var row = new JobOpeningRecruiter { JobOpeningId = o.Uid, UserId = userId, AssignedAt = clock.Today() };
        o.Recruiters.Add(row);
        if (log)
            AddEvent(db, ctx, clock, o, OpeningEventKind.RecruiterAssigned, recruiter: userId);
        return row;
    }

    /// <summary>
    /// Makes an assigned recruiter líder; the previous líder stays as apoyo. Saves the demotion first: the one-líder index is
    /// partial, so it cannot be deferred to the end of the statement batch. Run inside a transaction.
    /// </summary>
    public static async Task MakeLeadAsync(DbContext db, RequestContext ctx, IClock clock, JobOpening o, Guid userId, CancellationToken ct)
    {
        var row = Assign(db, ctx, clock, o, userId, log: false);
        if (row.IsLead)
            return;
        if (o.Recruiters.FirstOrDefault(r => r.IsLead) is { } previous)
        {
            previous.IsLead = false;
            await db.SaveChangesAsync(ct);
        }
        row.IsLead = true;
        o.LeadRecruiterId = userId;
        AddEvent(db, ctx, clock, o, OpeningEventKind.LeadChanged, recruiter: userId);
    }

    /// <summary>«Quitar»: a líder is replaced by <paramref name="newLeadId"/>, required while other recruiters remain (D71).</summary>
    public static async Task<IResult?> RemoveAsync(DbContext db, RequestContext ctx, IClock clock, JobOpening o, Guid userId,
        Guid? newLeadId, CancellationToken ct)
    {
        if (o.Recruiters.FirstOrDefault(r => r.UserId == userId) is not { } row)
            return ApiResults.NotFound("Ese reclutador no está asignado a la vacante.");
        var others = o.Recruiters.Where(r => r.UserId != userId).ToList();
        if (row.IsLead && others.Count > 0)
        {
            if (newLeadId is not { } next || others.All(r => r.UserId != next))
                return ApiResults.Validation(new Dictionary<string, string[]>
                {
                    ["newLeadId"] = ["Elige el nuevo líder entre los reclutadores asignados."],
                });
            o.Recruiters.Remove(row);
            o.LeadRecruiterId = null;
            AddEvent(db, ctx, clock, o, OpeningEventKind.RecruiterRemoved, recruiter: userId);
            await db.SaveChangesAsync(ct);
            await MakeLeadAsync(db, ctx, clock, o, next, ct);
            return null;
        }
        o.Recruiters.Remove(row);
        if (row.IsLead)
            o.LeadRecruiterId = null;
        AddEvent(db, ctx, clock, o, OpeningEventKind.RecruiterRemoved, recruiter: userId);
        return null;
    }
}
