using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Security.Cryptography;

namespace PamGateway.Api;

public interface IRecordingStorage
{
    Task<RecordingSaveResult> SaveAsync(string recordingId, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageUri, CancellationToken cancellationToken);
}

public sealed record RecordingSaveResult(
    string StorageUri,
    long SizeBytes,
    string Hash);

public sealed class LocalRecordingStorage : IRecordingStorage
{
    private readonly RecordingStorageOptions _options;

    public LocalRecordingStorage(RecordingStorageOptions options)
    {
        _options = options;
    }

    public async Task<RecordingSaveResult> SaveAsync(string recordingId, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.LocalPath);
        var tempPath = Path.Combine(Path.GetTempPath(), $"rec-{recordingId}-{Guid.NewGuid():N}.tmp");
        long size;
        string hash;

        using (var tempStream = File.Create(tempPath))
        using (var sha = SHA256.Create())
        using (var crypto = new CryptoStream(tempStream, sha, CryptoStreamMode.Write))
        {
            size = await CopyAsync(content, crypto, cancellationToken);
            crypto.FlushFinalBlock();
            hash = Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        }

        var basePath = Path.GetFullPath(_options.LocalPath);
        var finalPath = Path.Combine(basePath, $"{recordingId}.bin");
        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }
        File.Move(tempPath, finalPath);

        var uri = new Uri(finalPath).AbsoluteUri;
        return new RecordingSaveResult(uri, size, hash);
    }

    public Task<Stream> OpenReadAsync(string storageUri, CancellationToken cancellationToken)
    {
        var path = new Uri(storageUri).LocalPath;
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private static Task<long> CopyAsync(Stream input, Stream output, CancellationToken cancellationToken)
        => RecordingStorageHelpers.CopyAsync(input, output, cancellationToken);
}

public sealed class S3RecordingStorage : IRecordingStorage
{
    private readonly RecordingStorageOptions _options;
    private readonly IAmazonS3 _client;

    public S3RecordingStorage(RecordingStorageOptions options)
    {
        _options = options;
        var s3 = options.S3;
        var config = new AmazonS3Config
        {
            ForcePathStyle = s3.ForcePathStyle
        };

        if (!string.IsNullOrWhiteSpace(s3.Endpoint))
        {
            config.ServiceURL = s3.Endpoint;
            config.UseHttp = !s3.UseSsl;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3.Region);
        }

        if (!string.IsNullOrWhiteSpace(s3.AccessKey))
        {
            _client = new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), config);
        }
        else
        {
            _client = new AmazonS3Client(config);
        }
    }

    public async Task<RecordingSaveResult> SaveAsync(string recordingId, Stream content, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"rec-{recordingId}-{Guid.NewGuid():N}.tmp");
        long size;
        string hash;

        using (var tempStream = File.Create(tempPath))
        using (var sha = SHA256.Create())
        using (var crypto = new CryptoStream(tempStream, sha, CryptoStreamMode.Write))
        {
            size = await RecordingStorageHelpers.CopyAsync(content, crypto, cancellationToken);
            crypto.FlushFinalBlock();
            hash = Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        }

        var key = $"recordings/{recordingId}.bin";
        await using (var fileStream = File.OpenRead(tempPath))
        {
            var request = new PutObjectRequest
            {
                BucketName = _options.S3.Bucket,
                Key = key,
                InputStream = fileStream
            };
            await _client.PutObjectAsync(request, cancellationToken);
        }

        File.Delete(tempPath);
        var uri = $"s3://{_options.S3.Bucket}/{key}";
        return new RecordingSaveResult(uri, size, hash);
    }

    public async Task<Stream> OpenReadAsync(string storageUri, CancellationToken cancellationToken)
    {
        var (_, bucket, key) = ParseS3Uri(storageUri);
        var response = await _client.GetObjectAsync(bucket, key, cancellationToken);
        return new ResponseStreamWrapper(response);
    }

    private static (string scheme, string bucket, string key) ParseS3Uri(string uri)
    {
        var parsed = new Uri(uri);
        var bucket = parsed.Host;
        var key = parsed.AbsolutePath.TrimStart('/');
        return (parsed.Scheme, bucket, key);
    }

    private sealed class ResponseStreamWrapper : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;

        public ResponseStreamWrapper(GetObjectResponse response)
        {
            _response = response;
            _inner = response.ResponseStream;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.WriteAsync(buffer, offset, count, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _response.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

internal static class RecordingStorageHelpers
{
    public static async Task<long> CopyAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
        }

        return total;
    }
}
