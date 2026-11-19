using System.Text.Json;
using Platform.Core.Access;

namespace Platform.Core.Audit;

/// <summary>Postgres enum <c>audit_action</c>.</summary>
public enum AuditAction
{
    Insert,
    Update,
    Delete,
    Unlock,
    Export,
    Login,
}

/// <summary>Postgres enum <c>data_access_kind</c>.</summary>
public enum DataAccessKind
{
    ViewProfile,
    ViewDocument,
    DownloadDocument,
    DecryptAccount,
}

/// <summary>
/// DATA_MODEL §7 <c>audit_log</c>: append-only (a trigger rejects UPDATE/DELETE), partitioned by month, written in the
/// same transaction as the change. Log rows carry <c>occurred_at</c>/<c>user_id</c> instead of the four stamp columns (PLAN D53).
/// </summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ActingForUserId { get; set; }
    public AuditAction Action { get; set; }
    public required string SchemaName { get; set; }
    public required string TableName { get; set; }
    public Guid? RowId { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? RequestId { get; set; }
    public System.Net.IPAddress? Ip { get; set; }

    /// <summary>The audit row of a file export: who (and for whom, in cover mode), which table's data, and what was asked for (<paramref name="after"/>, serialized as JSON).</summary>
    public static AuditEntry ForExport(RequestContext ctx, DateTimeOffset now, string table, object after) => new()
    {
        OccurredAt = now,
        UserId = ctx.RequiredPrincipal.UserId,
        ActingForUserId = ctx.ActingFor?.UserId,
        Action = AuditAction.Export,
        SchemaName = "platform",
        TableName = table,
        After = JsonSerializer.Serialize(after),
        RequestId = ctx.RequestId,
    };
}

/// <summary>DATA_MODEL §7 <c>data_access_log</c>: who viewed or downloaded which personal data.</summary>
public sealed class DataAccessEntry
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ActingForUserId { get; set; }
    public Guid PersonId { get; set; }
    public Guid? DocumentId { get; set; }
    public DataAccessKind Access { get; set; }
    public string? RequestId { get; set; }
}
