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
/// <c>WHEN_REQUIRED</c> so MinIO, GCS interoperability and Spaces accept the requests.
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

    public async Task PingAsync(CancellationToken cancellationToken)
    {
        foreach (var bucket in new[] { settings.DocumentsBucket, settings.ExportsBucket })
            await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, MaxKeys = 1 }, cancellationToken);
    }

    public void Dispose() => _client.Dispose();
}
