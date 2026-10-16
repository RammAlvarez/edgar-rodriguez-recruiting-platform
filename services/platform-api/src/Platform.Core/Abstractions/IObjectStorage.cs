namespace Platform.Core.Abstractions;

/// <summary>S3-compatible object storage. Keys are always decided by the API; objects are private (presigned URLs only).</summary>
public interface IObjectStorage
{
    /// <summary>Throws when the configured buckets are unreachable (used by <c>/health</c>).</summary>
    Task PingAsync(CancellationToken cancellationToken);
}
