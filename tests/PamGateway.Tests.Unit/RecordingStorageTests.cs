using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Unit;

public sealed class RecordingStorageTests : IDisposable
{
    private readonly string _tempDir;

    public RecordingStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pam-rec-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task SaveAndRead_PlainNoCompression()
    {
        var storage = CreateStorage(compress: false, encrypt: false);
        var data = Encoding.UTF8.GetBytes("Hello, PAM Gateway recording test!");
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-plain", input, CancellationToken.None);

        result.StorageUri.Should().NotBeNullOrEmpty();
        result.SizeBytes.Should().Be(data.Length);
        result.Hash.Should().NotBeNullOrEmpty();

        await using var readStream = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SaveAndRead_WithGzipCompression()
    {
        var storage = CreateStorage(compress: true, encrypt: false);
        var data = Encoding.UTF8.GetBytes(new string('X', 10_000));
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-gzip", input, CancellationToken.None);

        result.StorageUri.Should().Contain(".gz");
        result.SizeBytes.Should().Be(data.Length);

        await using var readStream = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SaveAndRead_WithEncryption()
    {
        var key = GenerateBase64Key(32);
        var storage = CreateStorage(compress: false, encrypt: true, encryptionKey: key);
        var data = Encoding.UTF8.GetBytes("Encrypted content for PAM recording");
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-enc", input, CancellationToken.None);

        result.Hash.Should().NotBeNullOrEmpty();

        await using var readStream = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SaveAndRead_WithEncryptionAndCompression()
    {
        var key = GenerateBase64Key(32);
        var storage = CreateStorage(compress: true, encrypt: true, encryptionKey: key);
        var data = Encoding.UTF8.GetBytes(new string('Y', 5_000));
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-enc-gz", input, CancellationToken.None);

        result.StorageUri.Should().Contain(".gz");

        await using var readStream = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task HashVerification_MatchesAfterSave()
    {
        var storage = CreateStorage(compress: false, encrypt: false);
        var data = Encoding.UTF8.GetBytes("Verify hash integrity");
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-hash", input, CancellationToken.None);

        using var sha = SHA256.Create();
        var expected = Convert.ToHexString(sha.ComputeHash(data));
        result.Hash.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task HashVerification_DetectsCorruption()
    {
        var storage = CreateStorage(compress: false, encrypt: false);
        var data = Encoding.UTF8.GetBytes("Original content");
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-corrupt", input, CancellationToken.None);

        var filePath = new Uri(result.StorageUri).LocalPath;
        var corrupted = Encoding.UTF8.GetBytes("Tampered content!!!");
        await File.WriteAllBytesAsync(filePath, corrupted);

        await using var readStream = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        using var sha = SHA256.Create();
        var actualHash = Convert.ToHexString(sha.ComputeHash(ms.ToArray()));

        actualHash.Should().NotBeEquivalentTo(result.Hash, "corrupted file should have different hash");
    }

    [Fact]
    public async Task GzipCompression_ReducesFileSize()
    {
        var data = Encoding.UTF8.GetBytes(new string('A', 50_000));

        var plainStorage = CreateStorage(compress: false, encrypt: false);
        using var plainInput = new MemoryStream(data);
        var plainResult = await plainStorage.SaveAsync("rec-plain-size", plainInput, CancellationToken.None);
        var plainFileSize = new FileInfo(new Uri(plainResult.StorageUri).LocalPath).Length;

        var gzipStorage = CreateStorage(compress: true, encrypt: false);
        using var gzipInput = new MemoryStream(data);
        var gzipResult = await gzipStorage.SaveAsync("rec-gzip-size", gzipInput, CancellationToken.None);
        var gzipFileSize = new FileInfo(new Uri(gzipResult.StorageUri).LocalPath).Length;

        gzipFileSize.Should().BeLessThan(plainFileSize, "compressed file should be smaller");
    }

    [Fact]
    public async Task ChunkedUpload_ProducesSameResult()
    {
        var storage = CreateStorage(compress: false, encrypt: false);
        var chunk1 = Encoding.UTF8.GetBytes("Chunk 1 data. ");
        var chunk2 = Encoding.UTF8.GetBytes("Chunk 2 data. ");
        var chunk3 = Encoding.UTF8.GetBytes("Chunk 3 data.");

        using (var s1 = new MemoryStream(chunk1))
            await storage.SaveChunkAsync("rec-chunk", 0, s1, CancellationToken.None);
        using (var s2 = new MemoryStream(chunk2))
            await storage.SaveChunkAsync("rec-chunk", 1, s2, CancellationToken.None);
        using (var s3 = new MemoryStream(chunk3))
            await storage.SaveChunkAsync("rec-chunk", 2, s3, CancellationToken.None);

        var result = await storage.FinalizeChunksAsync("rec-chunk", 3, CancellationToken.None);

        var fullData = chunk1.Concat(chunk2).Concat(chunk3).ToArray();
        await using var readStream = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(fullData);
    }

    [Fact]
    public async Task FinalizeChunks_ThrowsOnMissingChunk()
    {
        var storage = CreateStorage(compress: false, encrypt: false);
        using (var s = new MemoryStream(new byte[] { 1, 2, 3 }))
            await storage.SaveChunkAsync("rec-missing", 0, s, CancellationToken.None);

        var act = () => storage.FinalizeChunksAsync("rec-missing", 3, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void EncryptionKey_MustBeValid()
    {
        var act = () => CreateStorage(compress: false, encrypt: true, encryptionKey: "not-base64!");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EncryptionKey_MustHaveValidLength()
    {
        var shortKey = Convert.ToBase64String(new byte[10]);
        var act = () => CreateStorage(compress: false, encrypt: true, encryptionKey: shortKey);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*128/192/256-bit*");
    }

    private LocalRecordingStorage CreateStorage(bool compress, bool encrypt, string? encryptionKey = null)
    {
        var options = new RecordingStorageOptions
        {
            Provider = "Local",
            LocalPath = _tempDir,
            EnableCompression = compress,
            Encryption = new RecordingStorageEncryptionOptions
            {
                Enabled = encrypt,
                Key = encryptionKey ?? ""
            }
        };
        return new LocalRecordingStorage(options);
    }

    private static string GenerateBase64Key(int bytes)
    {
        var key = new byte[bytes];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}
