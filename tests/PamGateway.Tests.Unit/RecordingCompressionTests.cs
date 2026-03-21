using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using PamGateway.Api;

namespace PamGateway.Tests.Unit;

public sealed class RecordingCompressionTests : IDisposable
{
    private readonly string _tempDir;

    public RecordingCompressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pam-rec-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private LocalRecordingStorage CreateStorage(bool compression = false, bool encryption = false, string? encKey = null)
    {
        var opts = new RecordingStorageOptions
        {
            Provider = "Local",
            LocalPath = _tempDir,
            EnableCompression = compression,
            Encryption = new RecordingStorageEncryptionOptions
            {
                Enabled = encryption,
                Key = encKey ?? ""
            }
        };
        return new LocalRecordingStorage(opts);
    }

    [Fact]
    public async Task SaveAndRead_WithoutCompression_RoundTrips()
    {
        var storage = CreateStorage();
        var data = "Hello, PAM recordings!"u8.ToArray();
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-1", input, CancellationToken.None);

        result.SizeBytes.Should().Be(data.Length);
        result.Hash.Should().NotBeNullOrWhiteSpace();

        await using var output = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await output.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SaveAndRead_WithCompression_RoundTrips()
    {
        var storage = CreateStorage(compression: true);
        var data = Encoding.UTF8.GetBytes(new string('A', 10_000));
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-compressed", input, CancellationToken.None);

        result.SizeBytes.Should().Be(data.Length);
        result.Hash.Should().NotBeNullOrWhiteSpace();
        result.StorageUri.Should().Contain(".bin.gz");

        var compressedFile = new Uri(result.StorageUri).LocalPath;
        new FileInfo(compressedFile).Length.Should().BeLessThan(data.Length);

        await using var output = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await output.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task HashVerification_MatchesAfterSave()
    {
        var storage = CreateStorage();
        var data = "integrity check data"u8.ToArray();
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-hash", input, CancellationToken.None);

        using var sha = SHA256.Create();
        var expectedHash = Convert.ToHexString(sha.ComputeHash(data));
        result.Hash.Should().BeEquivalentTo(expectedHash);
    }

    [Fact]
    public async Task ChunkedUpload_AssemblesCorrectly()
    {
        var storage = CreateStorage();
        var chunk0 = "chunk zero data"u8.ToArray();
        var chunk1 = " and chunk one"u8.ToArray();

        await storage.SaveChunkAsync("rec-chunked", 0, new MemoryStream(chunk0), CancellationToken.None);
        await storage.SaveChunkAsync("rec-chunked", 1, new MemoryStream(chunk1), CancellationToken.None);

        var result = await storage.FinalizeChunksAsync("rec-chunked", 2, CancellationToken.None);

        result.SizeBytes.Should().Be(chunk0.Length + chunk1.Length);

        await using var output = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await output.CopyToAsync(ms);
        var combined = Encoding.UTF8.GetString(ms.ToArray());
        combined.Should().Be("chunk zero data and chunk one");
    }

    [Fact]
    public async Task ChunkedUpload_WithCompression_RoundTrips()
    {
        var storage = CreateStorage(compression: true);
        var chunk0 = Encoding.UTF8.GetBytes(new string('X', 5000));
        var chunk1 = Encoding.UTF8.GetBytes(new string('Y', 5000));

        await storage.SaveChunkAsync("rec-chunk-gz", 0, new MemoryStream(chunk0), CancellationToken.None);
        await storage.SaveChunkAsync("rec-chunk-gz", 1, new MemoryStream(chunk1), CancellationToken.None);

        var result = await storage.FinalizeChunksAsync("rec-chunk-gz", 2, CancellationToken.None);

        await using var output = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await output.CopyToAsync(ms);
        var text = Encoding.UTF8.GetString(ms.ToArray());
        text.Should().Be(new string('X', 5000) + new string('Y', 5000));
    }

    [Fact]
    public async Task FinalizeChunks_MissingChunk_Throws()
    {
        var storage = CreateStorage();
        await storage.SaveChunkAsync("rec-missing", 0, new MemoryStream("data"u8.ToArray()), CancellationToken.None);

        var act = () => storage.FinalizeChunksAsync("rec-missing", 3, CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task SaveAndRead_WithCompressionAndEncryption_RoundTrips()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var storage = CreateStorage(compression: true, encryption: true, encKey: Convert.ToBase64String(key));

        var data = Encoding.UTF8.GetBytes(new string('Z', 8000));
        using var input = new MemoryStream(data);

        var result = await storage.SaveAsync("rec-enc-gz", input, CancellationToken.None);

        result.SizeBytes.Should().Be(data.Length);

        await using var output = await storage.OpenReadAsync(result.StorageUri, CancellationToken.None);
        using var ms = new MemoryStream();
        await output.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(data);
    }
}
