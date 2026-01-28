namespace PamGateway.Core;

public sealed class ObservabilityOptions
{
    public bool Enabled { get; set; } = false;
    public string? OtlpEndpoint { get; set; }
}
