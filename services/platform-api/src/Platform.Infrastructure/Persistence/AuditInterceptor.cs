using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;

namespace Platform.Infrastructure.Persistence;

/// <summary>
/// On every SaveChanges: assigns <c>uid</c> to new rows, stamps <see cref="IStamped"/> columns, and adds one
/// <see cref="AuditEntry"/> per inserted/updated/deleted row with before/after JSON — in the same transaction as the change.
/// Internal <c>id</c> columns and encrypted (bytea) values never enter the log.
/// </summary>
public sealed class AuditInterceptor(RequestContext request, IClock clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } db)
            Apply(db);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } db)
            Apply(db);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext db)
    {
        var now = clock.UtcNow;
        var userId = request.Principal?.UserId;
        var entries = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditEntry and not DataAccessEntry)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("Uid") is { } uidProp
                && (Guid)entry.Property(uidProp.Name).CurrentValue! == Guid.Empty)
                entry.Property(uidProp.Name).CurrentValue = Guid.CreateVersion7(now);

            if (entry.Entity is IStamped stamped)
            {
                if (entry.State == EntityState.Added)
                {
                    stamped.CreatedAt = now;
                    stamped.CreatedBy = userId;
                }
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    stamped.UpdatedAt = now;
                    stamped.UpdatedBy = userId;
                }
            }
        }

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified && !entry.Properties.Any(p => p.IsModified && !IsStamp(p)))
                continue;
            db.Add(new AuditEntry
            {
                OccurredAt = now,
                UserId = userId,
                ActingForUserId = request.ActingFor?.UserId,
                Action = entry.State switch
                {
                    EntityState.Added => AuditAction.Insert,
                    EntityState.Deleted => AuditAction.Delete,
                    _ => AuditAction.Update,
                },
                SchemaName = entry.Metadata.GetSchema() ?? PlatformDbContext.Schema,
                TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
                RowId = entry.Metadata.FindProperty("Uid") is { } p ? (Guid?)entry.Property(p.Name).CurrentValue : null,
                Before = entry.State == EntityState.Added ? null : Snapshot(entry, original: true),
                After = entry.State == EntityState.Deleted ? null : Snapshot(entry, original: false),
                RequestId = request.RequestId,
            });
        }
    }

    private static bool IsStamp(PropertyEntry p) => p.Metadata.Name is "UpdatedAt" or "UpdatedBy";

    private static string Snapshot(EntityEntry entry, bool original)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in entry.Properties)
        {
            if (p.Metadata.Name == "Id")
                continue;
            var column = p.Metadata.GetColumnName();
            var value = original ? p.OriginalValue : p.CurrentValue;
            values[column] = value is byte[] ? "[encrypted]" : value;
        }
        return JsonSerializer.Serialize(values, SnapshotJson);
    }

    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };
}
