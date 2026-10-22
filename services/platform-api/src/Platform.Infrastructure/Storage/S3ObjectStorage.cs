using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Platform.Core.Abstractions;

namespace Platform.Infrastructure.Storage;

/// <summary>Env: <c>S3_ENDPOINT</c>, <c>S3_REGION</c>, <c>S3_ACCESS_KEY_ID</c>, <c>S3_SECRET_ACCESS_KEY</c>, <c>S3_BUCKET_DOCUMENTS</c>, <c>S3_BUCKET_EXPORTS</c>, <c>S3_FORCE_PATH_STYLE</c>.</summary>
public sealed record S3Settings(
    string Endpoint,
    string Region,
    string AccessKeyId,
    string SecretAccessKey,
    string DocumentsBucket,
    string ExportsBucket,
    bool ForcePathStyle);

/// <summary>
/// The only place that references <c>AWSSDK.S3</c> (CI neutrality check). Checksum calculation/validation are
/// <c>WHEN_REQUIRED</c> so MinIO, GCS interoperability and Spaces accept the requests — and so presigned PUTs carry no
/// checksum header the browser would have to send.
/// </summary>
public sealed class S3ObjectStorage(S3Settings settings) : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client = new(
        new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey),
        new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            AuthenticationRegion = settings.Region,
            ForcePathStyle = settings.ForcePathStyle,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });

    private readonly Protocol _protocol =
        settings.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS;

    public async Task PingAsync(CancellationToken cancellationToken)
    {
        foreach (var bucket in new[] { settings.DocumentsBucket, settings.ExportsBucket })
            await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, MaxKeys = 1 }, cancellationToken);
    }

    public async Task<PresignedRequest> PresignUploadAsync(StorageBucket bucket, string key, string contentType, long sizeBytes,
        TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var expires = DateTime.UtcNow.Add(lifetime);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Name(bucket),
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expires,
            Protocol = _protocol,
        };
        // Signed, so the object can only be exactly this long. The browser sets Content-Length itself from the body.
        request.Headers["Content-Length"] = sizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = await _client.GetPreSignedURLAsync(request);
        return new PresignedRequest(url, new Dictionary<string, string> { ["Content-Type"] = contentType }, expires);
    }

    public async Task<PresignedRequest> PresignDownloadAsync(StorageBucket bucket, string key, string? downloadAs, TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var expires = DateTime.UtcNow.Add(lifetime);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Name(bucket),
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expires,
            Protocol = _protocol,
        };
        request.ResponseHeaderOverrides.ContentDisposition = downloadAs is null
            ? "inline"
            : $"attachment; filename=\"{AsciiFileName(downloadAs)}\"; filename*=UTF-8''{Uri.EscapeDataString(downloadAs)}";
        return new PresignedRequest(await _client.GetPreSignedURLAsync(request), new Dictionary<string, string>(), expires);
    }

    public async Task UploadAsync(StorageBucket bucket, string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream(content, writable: false);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Name(bucket),
            Key = key,
            InputStream = body,
            ContentType = contentType,
            // Unsigned streaming chunks are not accepted by every S3-compatible provider; a buffered body needs none.
            UseChunkEncoding = false,
        }, cancellationToken);
    }

    public async Task<StoredObject?> HeadAsync(StorageBucket bucket, string key, CancellationToken cancellationToken)
    {
        try
        {
            var meta = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = Name(bucket), Key = key },
                cancellationToken);
            return new StoredObject(meta.ContentLength, meta.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadPrefixAsync(StorageBucket bucket, string key, int count, CancellationToken cancellationToken)
    {
        using var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = Name(bucket),
            Key = key,
            ByteRange = new ByteRange(0, count - 1),
        }, cancellationToken);
        var buffer = new byte[count];
        var read = 0;
        while (read < count && await response.ResponseStream.ReadAsync(buffer.AsMemory(read, count - read), cancellationToken) is var n and > 0)
            read += n;
        return buffer[..read];
    }

    public async Task CopyAsync(StorageBucket bucket, string sourceKey, string destinationKey, CancellationToken cancellationToken) =>
        await _client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = Name(bucket),
            SourceKey = sourceKey,
            DestinationBucket = Name(bucket),
            DestinationKey = destinationKey,
            MetadataDirective = S3MetadataDirective.COPY,
        }, cancellationToken);

    public Task DeleteAsync(StorageBucket bucket, string key, CancellationToken cancellationToken) =>
        _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Name(bucket), Key = key }, cancellationToken);

    private string Name(StorageBucket bucket) => bucket == StorageBucket.Documents ? settings.DocumentsBucket : settings.ExportsBucket;

    /// <summary>The plain name old clients read: accents drop to their letter («OPERACIÓN» → «OPERACION»), anything else non-ASCII is `_`.</summary>
    public static string AsciiFileName(string name) =>
        new(name.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => c is >= ' ' and <= '~' && c is not '"' and not '\\' ? c : '_').ToArray());

    public void Dispose() => _client.Dispose();
}
