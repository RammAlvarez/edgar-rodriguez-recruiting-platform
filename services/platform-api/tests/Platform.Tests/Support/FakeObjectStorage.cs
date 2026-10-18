using System.Collections.Concurrent;
using Platform.Core.Abstractions;

namespace Platform.Tests.Support;

/// <summary>
/// In-memory bucket for the API tests: presigned URLs are fake, and a test "uploads" with <see cref="Put"/>. The real
/// S3 adapter is tested against the local MinIO in <c>S3ObjectStorageTests</c>.
/// </summary>
public sealed class FakeObjectStorage : IObjectStorage
{
    public ConcurrentDictionary<(StorageBucket, string), StoredObject> Objects { get; } = new();

    public ConcurrentBag<(StorageBucket Bucket, string Key, string? DownloadAs)> Downloads { get; } = [];

    /// <summary>An upload; without <paramref name="head"/> its first bytes are a valid signature of <paramref name="contentType"/>.</summary>
    public void Put(string key, long size, string contentType, byte[]? head = null)
    {
        Objects[(StorageBucket.Documents, key)] = new StoredObject(size, contentType);
        Contents[(StorageBucket.Documents, key)] = head ?? contentType switch
        {
            "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            "image/jpeg" => [0xFF, 0xD8, 0xFF, 0xE0],
            _ => "%PDF-1.4"u8.ToArray(),
        };
    }

    public Task PingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<PresignedRequest> PresignUploadAsync(StorageBucket bucket, string key, string contentType, TimeSpan lifetime,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PresignedRequest($"https://storage.test/{bucket}/{key}?put",
            new Dictionary<string, string> { ["Content-Type"] = contentType }, DateTimeOffset.UtcNow.Add(lifetime)));

    public Task<PresignedRequest> PresignDownloadAsync(StorageBucket bucket, string key, string? downloadAs, TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        Downloads.Add((bucket, key, downloadAs));
        return Task.FromResult(new PresignedRequest($"https://storage.test/{bucket}/{key}?get", new Dictionary<string, string>(),
            DateTimeOffset.UtcNow.Add(lifetime)));
    }

    /// <summary>What the API wrote with <see cref="UploadAsync"/> (exports), by bucket and key.</summary>
    public ConcurrentDictionary<(StorageBucket, string), byte[]> Contents { get; } = new();

    public Task UploadAsync(StorageBucket bucket, string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        Contents[(bucket, key)] = content;
        Objects[(bucket, key)] = new StoredObject(content.Length, contentType);
        return Task.CompletedTask;
    }

    public Task<StoredObject?> HeadAsync(StorageBucket bucket, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Objects.TryGetValue((bucket, key), out var o) ? o : null);

    public Task<byte[]> ReadPrefixAsync(StorageBucket bucket, string key, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Contents.TryGetValue((bucket, key), out var c) ? c[..Math.Min(count, c.Length)] : []);

    public Task DeleteAsync(StorageBucket bucket, string key, CancellationToken cancellationToken)
    {
        Objects.TryRemove((bucket, key), out _);
        return Task.CompletedTask;
    }
}
