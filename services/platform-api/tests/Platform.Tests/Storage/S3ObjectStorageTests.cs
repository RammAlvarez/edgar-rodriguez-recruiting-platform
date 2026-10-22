using System.Net;
using System.Net.Http.Headers;
using Platform.Core.Abstractions;
using Platform.Infrastructure.Storage;

namespace Platform.Tests.Storage;

/// <summary>
/// The real S3 adapter against the local MinIO (compose, and a service container in CI): the presigned PUT the browser
/// performs, HEAD, the presigned GET with a download name, and delete. Proves the WHEN_REQUIRED checksum setting.
/// </summary>
public sealed class S3ObjectStorageTests : IDisposable
{
    private readonly S3ObjectStorage _storage = new(new S3Settings(
        Environment.GetEnvironmentVariable("S3_ENDPOINT") ?? "http://localhost:9000", "us-east-1", "minioadmin", "minioadmin",
        "documents", "exports", ForcePathStyle: true));

    private readonly HttpClient _browser = new();

    [Fact]
    public async Task A_presigned_put_uploads_and_a_presigned_get_downloads_with_the_original_name()
    {
        var key = $"tests/{Guid.NewGuid()}.pdf";
        var bytes = "%PDF-1.4 prueba"u8.ToArray();

        var put = await _storage.PresignUploadAsync(StorageBucket.Documents, key, "application/pdf", bytes.Length, TimeSpan.FromMinutes(5), default);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(put.Headers["Content-Type"]);
        Assert.Equal(HttpStatusCode.OK, (await _browser.PutAsync(put.Url, content)).StatusCode);

        var head = await _storage.HeadAsync(StorageBucket.Documents, key, default);
        Assert.Equal(bytes.Length, head?.SizeBytes);
        Assert.Equal("application/pdf", head?.ContentType);

        var get = await _storage.PresignDownloadAsync(StorageBucket.Documents, key, "INE frente.pdf", TimeSpan.FromMinutes(5), default);
        var download = await _browser.GetAsync(get.Url);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);

        await _storage.DeleteAsync(StorageBucket.Documents, key, default);
        Assert.Null(await _storage.HeadAsync(StorageBucket.Documents, key, default));
    }

    [Fact]
    public async Task An_export_the_api_writes_is_downloaded_through_a_presigned_get()
    {
        var key = $"tests/{Guid.NewGuid()}.xlsx";
        var bytes = "PK reporte"u8.ToArray();
        const string type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        await _storage.UploadAsync(StorageBucket.Exports, key, bytes, type, default);
        Assert.Equal(type, (await _storage.HeadAsync(StorageBucket.Exports, key, default))?.ContentType);
        var get = await _storage.PresignDownloadAsync(StorageBucket.Exports, key, "Rendimiento por reclutador.xlsx", TimeSpan.FromMinutes(5), default);
        Assert.Equal(bytes, await (await _browser.GetAsync(get.Url)).Content.ReadAsByteArrayAsync());
        // The first bytes only (the document signature check, D-027); a longer count returns what there is.
        Assert.Equal("PK"u8.ToArray(), await _storage.ReadPrefixAsync(StorageBucket.Exports, key, 2, default));
        Assert.Equal(bytes, await _storage.ReadPrefixAsync(StorageBucket.Exports, key, 64, default));

        await _storage.DeleteAsync(StorageBucket.Exports, key, default);
    }

    [Fact]
    public async Task A_put_with_another_content_type_than_the_signed_one_is_refused()
    {
        var put = await _storage.PresignUploadAsync(StorageBucket.Documents, $"tests/{Guid.NewGuid()}.pdf", "application/pdf", 3,
            TimeSpan.FromMinutes(5), default);
        using var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        Assert.Equal(HttpStatusCode.Forbidden, (await _browser.PutAsync(put.Url, content)).StatusCode);
    }

    [Fact]
    public async Task A_put_longer_or_shorter_than_the_signed_size_is_refused()
    {
        foreach (var length in new[] { 4, 2 })
        {
            var put = await _storage.PresignUploadAsync(StorageBucket.Documents, $"tests/{Guid.NewGuid()}.pdf", "application/pdf", 3,
                TimeSpan.FromMinutes(5), default);
            using var content = new ByteArrayContent(new byte[length]);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(put.Headers["Content-Type"]);
            Assert.Equal(HttpStatusCode.Forbidden, (await _browser.PutAsync(put.Url, content)).StatusCode);
        }
    }

    [Fact]
    public async Task A_copy_is_a_snapshot_that_later_writes_to_the_source_do_not_change()
    {
        var source = $"tests/{Guid.NewGuid()}.pdf";
        var copy = $"tests/{Guid.NewGuid()}.pdf";
        await _storage.UploadAsync(StorageBucket.Documents, source, "%PDF-1.4 uno"u8.ToArray(), "application/pdf", default);
        await _storage.CopyAsync(StorageBucket.Documents, source, copy, default);
        await _storage.UploadAsync(StorageBucket.Documents, source, "MZ otro"u8.ToArray(), "text/html", default);

        var head = await _storage.HeadAsync(StorageBucket.Documents, copy, default);
        Assert.Equal("application/pdf", head?.ContentType);
        Assert.Equal("%PDF-1.4 uno"u8.ToArray(), await _storage.ReadPrefixAsync(StorageBucket.Documents, copy, 64, default));
        await _storage.DeleteAsync(StorageBucket.Documents, source, default);
        await _storage.DeleteAsync(StorageBucket.Documents, copy, default);
    }

    public void Dispose()
    {
        _storage.Dispose();
        _browser.Dispose();
    }
}
