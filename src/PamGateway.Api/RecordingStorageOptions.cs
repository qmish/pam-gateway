namespace PamGateway.Api;

public sealed class RecordingStorageOptions
{
    public string Provider { get; init; } = "Local";
    public string LocalPath { get; init; } = "/data/recordings";
    public S3StorageOptions S3 { get; init; } = new();
    public RecordingStorageEncryptionOptions Encryption { get; init; } = new();
    public bool EnableCompression { get; init; }
}

public sealed class S3StorageOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string Bucket { get; init; } = "pam-recordings";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Region { get; init; } = "us-east-1";
    public bool UseSsl { get; init; } = true;
    public bool ForcePathStyle { get; init; } = true;
}

public sealed class RecordingStorageEncryptionOptions
{
    public bool Enabled { get; init; }
    public string Key { get; init; } = string.Empty;
}
