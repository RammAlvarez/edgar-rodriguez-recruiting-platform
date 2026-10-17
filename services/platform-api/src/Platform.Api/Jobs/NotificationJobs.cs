using System.Net;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Internal;
using Platform.Core.Abstractions;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.Notifications;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Payroll.Groups;
using Platform.Payroll.Staff;
using Platform.Recruiting.Candidates;

namespace Platform.Api.Jobs;

/// <summary>
/// <c>document-expiry</c>, daily (SPEC §3.5, PLAN D88): a document whose expiry is 30 days away or less is told once at 30 and
/// once at 7 — to the recruiters of the person's applications and, for a current employee, to whoever has Personal for that
/// client —, and an open vacante with no activity for more than <see cref="IdleDays"/> days (D80's rule) to its líder, once per
/// idle stretch.
/// </summary>
public sealed class DocumentExpiryJob(DbContext db, IClock clock) : IJob
{
    public const int IdleDays = 7;

    public string Name => "document-expiry";

    private sealed record IdleOpening(Guid Id, Guid ClientId, string PositionName, Guid? LeadRecruiterId, DateTimeOffset LastActivityAt);

    public async Task<object> RunAsync(CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var now = clock.UtcNow;
        var today = clock.Today();
        var documents = await db.Set<PersonDocument>().AsNoTracking()
            .Where(d => d.SupersededBy == null && d.Status != DocumentStatus.Rejected && d.ExpiresAt != null && d.ExpiresAt >= today
                && d.ExpiresAt <= today.AddDays(30))
            .Join(db.Set<DocumentType>(), d => d.DocumentTypeId, t => t.Uid, (d, t) => new { d.Uid, d.PersonId, d.ExpiresAt, Type = t.NameEs })
            .Join(db.Set<Person>(), x => x.PersonId, p => p.Uid, (x, p) => new { x.Uid, x.PersonId, x.ExpiresAt, x.Type, p.FullName })
            .ToListAsync(ct);
        var told = 0;
        foreach (var d in documents)
        {
            var threshold = d.ExpiresAt!.Value <= today.AddDays(7) ? 7 : 30;
            var key = $"doc-expiring:{d.Uid}:{threshold}";
            var title = $"{d.Type} de {d.FullName} vence el {d.ExpiresAt:dd/MM/yyyy}";
            const string body = "Pide el documento vigente y súbelo antes de que venza.";
            foreach (var c in await db.Set<Candidate>().Where(c => c.PersonId == d.PersonId && c.RecruiterId != null)
                         .Select(c => new { c.Uid, Recruiter = c.RecruiterId!.Value }).ToListAsync(ct))
                told += await Notifier.NotifyAsync(db, [c.Recruiter],
                    new NotificationDraft(NotificationKind.DocumentExpiring, key, title, body, await CandidateLinkAsync(c.Recruiter, c.Uid, ct)), now, ct);
            foreach (var e in await db.Set<Employee>().Where(e => e.PersonId == d.PersonId && (e.TerminatedAt == null || e.TerminatedAt > today))
                         .Join(db.Set<PayrollGroup>(), e => e.PayrollGroupId, g => g.Uid, (e, g) => new { e.Uid, g.ClientId }).ToListAsync(ct))
                told += await Notifier.NotifyAsync(db, await Notifier.Holders(db, PermissionModule.PayrollStaff, e.ClientId).ToListAsync(ct),
                    new NotificationDraft(NotificationKind.DocumentExpiring, key, title, body, $"/personal/{e.Uid}"), now, ct);
        }

        var idleBefore = now.AddDays(-IdleDays);
        var idle = await db.Database.SqlQuery<IdleOpening>($"""
                SELECT id, client_id, position_name, lead_recruiter_id, last_activity_at
                FROM platform.open_openings_aging WHERE lead_recruiter_id IS NOT NULL AND last_activity_at < {idleBefore}
                """).ToListAsync(ct);
        var clients = await db.Set<Client>().ToDictionaryAsync(c => c.Uid, c => c.ShortName, ct);
        var idleTold = 0;
        foreach (var o in idle)
        {
            var days = (int)(now - o.LastActivityAt).TotalDays;
            idleTold += await Notifier.NotifyAsync(db, [o.LeadRecruiterId!.Value],
                new NotificationDraft(NotificationKind.OpeningIdle, $"opening-idle:{o.Id}:{o.LastActivityAt:yyyy-MM-dd}",
                    $"{clients.GetValueOrDefault(o.ClientId)} – {o.PositionName}: {days} días sin actividad",
                    "Nadie movió candidatos ni editó la vacante en más de una semana.", await OpeningLinkAsync(o.LeadRecruiterId.Value, o.Id, ct)), now, ct);
        }
        await db.SaveChangesAsync(ct);
        return new { documentsTold = told, idleOpeningsTold = idleTold };
    }

    private async Task<string> CandidateLinkAsync(Guid userId, Guid candidateId, CancellationToken ct) =>
        await IsRecruiterAsync(userId, ct) ? $"/mis-candidatos/{candidateId}" : $"/candidatos/{candidateId}";

    private async Task<string> OpeningLinkAsync(Guid userId, Guid openingId, CancellationToken ct) =>
        await IsRecruiterAsync(userId, ct) ? $"/mis-vacantes/{openingId}" : $"/vacantes/{openingId}";

    private Task<bool> IsRecruiterAsync(Guid userId, CancellationToken ct) =>
        db.Set<User>().AnyAsync(u => u.Uid == userId && u.Role == UserRole.Recruiter, ct);
}

/// <summary>
/// <c>notifications-digest</c>, daily (SPEC §3.5 «email only»): one email per user with the notifications of the last week
/// they have not read and that no digest has sent yet; each is emailed once.
/// </summary>
public sealed partial class NotificationsDigestJob(DbContext db, IClock clock, IEmailSender email, AppLinks links, ILogger<NotificationsDigestJob> log) : IJob
{
    public string Name => "notifications-digest";

    public async Task<object> RunAsync(CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var now = clock.UtcNow;
        var since = now.AddDays(-7);
        var pending = await db.Set<Notification>()
            .Where(n => n.ReadAt == null && n.EmailedAt == null && n.CreatedAt >= since)
            .Join(db.Set<User>().Where(u => u.IsActive), n => n.UserId, u => u.Uid, (n, u) => new { n, u.Email, u.DisplayName })
            .ToListAsync(ct);
        var sent = 0;
        foreach (var user in pending.GroupBy(x => (x.Email, x.DisplayName)))
        {
            var items = user.Select(x => x.n).OrderByDescending(n => n.CreatedAt).ToList();
            try
            {
                await email.SendAsync(Build(user.Key.Email, user.Key.DisplayName, items, links.Admin), ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // One failed address does not stop the others; its items go in tomorrow's digest.
                DigestNotSent(log, e);
                continue;
            }
            foreach (var n in items)
                n.EmailedAt = now;
            await db.SaveChangesAsync(ct);
            sent++;
        }
        return new { emailsSent = sent };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Digest not sent to a user")]
    private static partial void DigestNotSent(ILogger logger, Exception exception);

    public static EmailMessage Build(string to, string name, IReadOnlyList<Notification> items, string appUrl)
    {
        static string e(string value) => WebUtility.HtmlEncode(value);
        var url = appUrl.TrimEnd('/');
        var subject = items.Count == 1 ? $"Tienes 1 aviso: {items[0].Title}" : $"Tienes {items.Count} avisos en la plataforma";
        var text = $"Hola {name}:\n\n" + string.Join("\n", items.Select(n => $"- {n.Title}{(n.Body is null ? "" : $". {n.Body}")} {url}{n.Link}"))
            + $"\n\nEntra a {url} para verlos.";
        var html = $"<p>Hola {e(name)}:</p><ul>"
            + string.Join("", items.Select(n => $"<li><a href=\"{e(url + (n.Link ?? "/"))}\">{e(n.Title)}</a>{(n.Body is null ? "" : $"<br><span style=\"color:#555\">{e(n.Body)}</span>")}</li>"))
            + $"</ul><p style=\"color:#555\">Recibes este resumen una vez al día cuando tienes avisos sin leer.</p>";
        return new EmailMessage(to, subject, html, text);
    }
}
