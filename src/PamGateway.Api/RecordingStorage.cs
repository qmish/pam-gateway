using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Security.Cryptography;
using System.Text;

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
    private readonly RecordingStorageEncryptionOptions _encryption;
    private readonly byte[]? _encryptionKey;

    public LocalRecordingStorage(RecordingStorageOptions options)
    {
        _options = options;
        _encryption = options.Encryption;
        _encryptionKey = RecordingStorageHelpers.GetEncryptionKey(_encryption);
    }

    public async Task<RecordingSaveResult> SaveAsync(string recordingId, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.LocalPath);
        var tempPath = Path.Combine(Path.GetTempPath(), $"rec-{recordingId}-{Guid.NewGuid():N}.tmp");
        long size;
        string hash;

        using (var tempStream = File.Create(tempPath))
        using (var sha = SHA256.Create())
        {
            if (_encryption.Enabled)
            {
                var encryptionKey = _encryptionKey ?? throw new InvalidOperationException("Recording storage encryption key is not configured.");
                using var aes = RecordingStorageHelpers.CreateAes(encryptionKey);
                await RecordingStorageHelpers.WriteEncryptionHeaderAsync(tempStream, aes.IV, cancellationToken);
                await using var crypto = new CryptoStream(tempStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                size = await RecordingStorageHelpers.CopyWithHashAsync(content, crypto, sha, cancellationToken);
                crypto.FlushFinalBlock();
            }
            else
            {
                size = await RecordingStorageHelpers.CopyWithHashAsync(content, tempStream, sha, cancellationToken);
            }

            hash = RecordingStorageHelpers.ToHexHash(sha);
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

    public async Task<Stream> OpenReadAsync(string storageUri, CancellationToken cancellationToken)
    {
        var path = new Uri(storageUri).LocalPath;
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!_encryption.Enabled)
        {
            return stream;
        }

        var key = _encryptionKey ?? throw new InvalidOperationException("Recording storage encryption key is not configured.");
        return await RecordingStorageHelpers.CreateDecryptingStreamAsync(stream, key, cancellationToken);
    }
}

public sealed class S3RecordingStorage : IRecordingStorage
{
    private readonly RecordingStorageOptions _options;
    private readonly IAmazonS3 _client;
    private readonly RecordingStorageEncryptionOptions _encryption;
    private readonly byte[]? _encryptionKey;

    public S3RecordingStorage(RecordingStorageOptions options)
    {
        _options = options;
        _encryption = options.Encryption;
        _encryptionKey = RecordingStorageHelpers.GetEncryptionKey(_encryption);
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
        {
            if (_encryption.Enabled)
            {
                var key = _encryptionKey ?? throw new InvalidOperationException("Recording storage encryption key is not configured.");
                using var aes = RecordingStorageHelpers.CreateAes(key);
                await RecordingStorageHelpers.WriteEncryptionHeaderAsync(tempStream, aes.IV, cancellationToken);
                await using var crypto = new CryptoStream(tempStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true);
                size = await RecordingStorageHelpers.CopyWithHashAsync(content, crypto, sha, cancellationToken);
                crypto.FlushFinalBlock();
            }
            else
            {
                size = await RecordingStorageHelpers.CopyWithHashAsync(content, tempStream, sha, cancellationToken);
            }

            hash = RecordingStorageHelpers.ToHexHash(sha);
        }

        var objectKey = $"recordings/{recordingId}.bin";
        await using (var fileStream = File.OpenRead(tempPath))
        {
            var request = new PutObjectRequest
            {
                BucketName = _options.S3.Bucket,
                Key = objectKey,
                InputStream = fileStream
            };
            await _client.PutObjectAsync(request, cancellationToken);
        }

        File.Delete(tempPath);
        var uri = $"s3://{_options.S3.Bucket}/{objectKey}";
        return new RecordingSaveResult(uri, size, hash);
    }

    public async Task<Stream> OpenReadAsync(string storageUri, CancellationToken cancellationToken)
    {
        var (_, bucket, key) = ParseS3Uri(storageUri);
        var response = await _client.GetObjectAsync(bucket, key, cancellationToken);
        var responseStream = new ResponseStreamWrapper(response);
        if (!_encryption.Enabled)
        {
            return responseStream;
        }

        var encryptionKey = _encryptionKey ?? throw new InvalidOperationException("Recording storage encryption key is not configured.");
        return await RecordingStorageHelpers.CreateDecryptingStreamAsync(responseStream, encryptionKey, cancellationToken);
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
    private static readonly byte[] EncryptionMagic = Encoding.ASCII.GetBytes("PGREC1");

    public static byte[]? GetEncryptionKey(RecordingStorageEncryptionOptions options)
    {
        if (!options.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException("Recording storage encryption is enabled but the key is missing.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(options.Key);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Recording storage encryption key must be Base64 encoded.", ex);
        }

        if (key.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException("Recording storage encryption key must be 128/192/256-bit.");
        }

        return key;
    }

    public static Aes CreateAes(byte[] key, byte[]? iv = null)
    {
        var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        if (iv is null)
        {
            aes.GenerateIV();
        }
        else
        {
            aes.IV = iv;
        }
        return aes;
    }

    public static async Task WriteEncryptionHeaderAsync(Stream output, byte[] iv, CancellationToken cancellationToken)
    {
        await output.WriteAsync(EncryptionMagic, cancellationToken);
        await output.WriteAsync(iv, cancellationToken);
    }

    public static async Task<Stream> CreateDecryptingStreamAsync(Stream input, byte[] key, CancellationToken cancellationToken)
    {
        var header = new byte[EncryptionMagic.Length];
        await ReadExactlyAsync(input, header, cancellationToken);
        if (!header.AsSpan().SequenceEqual(EncryptionMagic))
        {
            throw new InvalidDataException("Recording content is not encrypted or has invalid header.");
        }

        var iv = new byte[16];
        await ReadExactlyAsync(input, iv, cancellationToken);

        var aes = CreateAes(key, iv);
        var decryptor = aes.CreateDecryptor();
        return new CryptoStream(new OwnedStream(input, aes), decryptor, CryptoStreamMode.Read);
    }

    public static async Task<long> CopyWithHashAsync(Stream input, Stream output, HashAlgorithm hash, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            hash.TransformBlock(buffer, 0, read, null, 0);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
        }

        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return total;
    }

    public static string ToHexHash(HashAlgorithm hash)
        => Convert.ToHexString(hash.Hash ?? Array.Empty<byte>());

    public static async Task ReadExactlyAsync(Stream input, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await input.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of recording stream.");
            }

            offset += read;
        }
    }

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

    private sealed class OwnedStream : Stream
    {
        private readonly Stream _inner;
        private readonly IDisposable _owner;

        public OwnedStream(Stream inner, IDisposable owner)
        {
            _inner = inner;
            _owner = owner;
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
                _inner.Dispose();
                _owner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
