namespace Platform.Core.Abstractions;

/// <summary>The two private buckets (ARCHITECTURE §2): person documents and generated exports.</summary>
public enum StorageBucket
{
    Documents,
    Exports,
}

/// <summary>A presigned request the browser performs itself; <see cref="Headers"/> must be sent exactly as given.</summary>
public sealed record PresignedRequest(string Url, IReadOnlyDictionary<string, string> Headers, DateTimeOffset ExpiresAt);

public sealed record StoredObject(long SizeBytes, string? ContentType);

/// <summary>S3-compatible object storage. Keys are always decided by the API; objects are private (presigned URLs only).</summary>
public interface IObjectStorage
{
    /// <summary>Throws when the configured buckets are unreachable (used by <c>/health</c>).</summary>
    Task PingAsync(CancellationToken cancellationToken);

    /// <summary>A PUT for one object; the content type is part of the signature.</summary>
    Task<PresignedRequest> PresignUploadAsync(StorageBucket bucket, string key, string contentType, TimeSpan lifetime,
        CancellationToken cancellationToken);

    /// <summary>A GET; <paramref name="downloadAs"/> sets <c>Content-Disposition: attachment</c> with that file name, otherwise inline.</summary>
    Task<PresignedRequest> PresignDownloadAsync(StorageBucket bucket, string key, string? downloadAs, TimeSpan lifetime,
        CancellationToken cancellationToken);

    /// <summary>Size and type of an uploaded object, or <c>null</c> when it does not exist.</summary>
    Task<StoredObject?> HeadAsync(StorageBucket bucket, string key, CancellationToken cancellationToken);

    Task DeleteAsync(StorageBucket bucket, string key, CancellationToken cancellationToken);
}
