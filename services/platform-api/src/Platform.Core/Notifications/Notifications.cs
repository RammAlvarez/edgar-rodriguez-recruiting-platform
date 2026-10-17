using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Http;
using Platform.Core.Persistence;
using Platform.Core.Users;

namespace Platform.Core.Notifications;

/// <summary>Postgres enum <c>notification_kind</c>: the triggers of SPEC §3.5 (PLAN D88).</summary>
public enum NotificationKind
{
    DocumentExpiring,
    DocumentUploaded,
    OpeningIdle,
    AttendanceMissing,
    PeriodInReview,
    UserInvited,
}

/// <summary>
/// DATA_MODEL §3.11 <c>notifications</c>, one item of a user's bell (SPEC §3.5). The text is written when it is created;
/// <see cref="DedupeKey"/> keeps a job that runs again from repeating it; <see cref="EmailedAt"/> marks it as sent in the daily
/// digest. System-made, so not audited (<see cref="IDerived"/>).
/// </summary>
public sealed class Notification : IDerived
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    /// <summary>The admin app path it opens («/prenomina?period=…»).</summary>
    public string? Link { get; set; }
    public required string DedupeKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? EmailedAt { get; set; }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasIdentityAndUid();
        b.Property(n => n.Title).HasMaxLength(160);
        b.Property(n => n.Body).HasMaxLength(500);
        b.Property(n => n.Link).HasMaxLength(300);
        b.Property(n => n.DedupeKey).HasMaxLength(200);
        b.HasIndex(n => new { n.UserId, n.DedupeKey }).IsUnique();
        b.HasIndex(n => new { n.UserId, n.ReadAt });
        b.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).HasPrincipalKey(u => u.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Told when a candidate uploads a document from the Candidates app (the document module is Core; who recruits the person is
/// the Recruiting module's, which implements this).
/// </summary>
public interface ICandidateDocumentListener
{
    Task UploadedAsync(DbContext db, Guid personId, Guid documentId, string documentName, DateTimeOffset now, CancellationToken ct);
}

/// <summary>What a module asks to tell: the same text to every recipient, once per <see cref="Key"/> and user.</summary>
public sealed record NotificationDraft(NotificationKind Kind, string Key, string Title, string? Body, string? Link);

public static class Notifier
{
    /// <summary>
    /// Adds the notification for each active user not yet told under the same key (not saved: it goes with the caller's
    /// transaction). Returns how many were added.
    /// </summary>
    public static async Task<int> NotifyAsync(DbContext db, IEnumerable<Guid> userIds, NotificationDraft draft, DateTimeOffset now,
        CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return 0;
        var told = await db.Set<Notification>().Where(n => ids.Contains(n.UserId) && n.DedupeKey == draft.Key).Select(n => n.UserId).ToListAsync(ct);
        var pending = db.ChangeTracker.Entries<Notification>().Where(e => e.Entity.DedupeKey == draft.Key).Select(e => e.Entity.UserId).ToHashSet();
        var active = await db.Set<User>().Where(u => ids.Contains(u.Uid) && u.IsActive).Select(u => u.Uid).ToListAsync(ct);
        var added = 0;
        foreach (var id in active.Except(told).Where(id => !pending.Contains(id)))
        {
            db.Add(new Notification
            {
                UserId = id,
                Kind = draft.Kind,
                Title = Cut(draft.Title, 160),
                Body = draft.Body is null ? null : Cut(draft.Body, 500),
                Link = draft.Link,
                DedupeKey = draft.Key,
                CreatedAt = now,
            });
            added++;
        }
        return added;
    }

    /// <summary>
    /// Active Administradores holding <paramref name="module"/> for <paramref name="clientId"/> (a grant for every client counts).
    /// </summary>
    public static IQueryable<Guid> Holders(DbContext db, PermissionModule module, Guid clientId) =>
        db.Set<UserPermission>().Where(p => p.Module == module && (p.ClientId == null || p.ClientId == clientId))
            .Join(db.Set<User>().Where(u => u.IsActive && u.Role == UserRole.Administrator), p => p.UserId, u => u.Uid, (_, u) => u.Uid)
            .Distinct();

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

public sealed record NotificationDto(Guid Id, NotificationKind Kind, string Title, string? Body, string? Link, DateTimeOffset CreatedAt,
    bool Read);

/// <summary>The bell: the latest items and how many are unread.</summary>
public sealed record NotificationsDto(int Unread, IReadOnlyList<NotificationDto> Items);

/// <summary>The bell (SPEC §3.5): the caller's own notifications only — staff of the admin app.</summary>
public static class NotificationEndpoints
{
    public const string Tag = "notifications";

    public static readonly AccessRule Access = AccessRule.ForRoles(UserRole.Administrator, UserRole.Recruiter);

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder v1)
    {
        var g = v1.MapGroup("/notifications").WithTags("Notifications");

        g.MapGet("", async (DbContext db, RequestContext ctx, CancellationToken ct) =>
            {
                var me = ctx.RequiredPrincipal.UserId;
                var mine = db.Set<Notification>().AsNoTracking().Where(n => n.UserId == me);
                var items = await mine.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(50)
                    .Select(n => new NotificationDto(n.Uid, n.Kind, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null)).ToListAsync(ct);
                return TypedResults.Ok(new NotificationsDto(await mine.CountAsync(n => n.ReadAt == null, ct), items));
            })
            .WithName("ListNotifications").Produces<NotificationsDto>().RequireAccess(Access);

        g.MapPost("/{id:guid}/read", async (Guid id, DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var me = ctx.RequiredPrincipal.UserId;
                var n = await db.Set<Notification>().SingleOrDefaultAsync(x => x.Uid == id && x.UserId == me, ct);
                if (n is null)
                    return ApiResults.NotFound("La notificación no existe.");
                n.ReadAt ??= clock.UtcNow;
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("ReadNotification").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound)
            .Invalidates(Tag).RequireAccess(Access);

        g.MapPost("/read-all", async (DbContext db, RequestContext ctx, IClock clock, CancellationToken ct) =>
            {
                var me = ctx.RequiredPrincipal.UserId;
                var now = clock.UtcNow;
                await db.Set<Notification>().Where(n => n.UserId == me && n.ReadAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
                return TypedResults.NoContent();
            })
            .WithName("ReadAllNotifications").Produces(StatusCodes.Status204NoContent).Invalidates(Tag).RequireAccess(Access);

        return v1;
    }
}
