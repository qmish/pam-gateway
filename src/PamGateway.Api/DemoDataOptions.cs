namespace PamGateway.Api;

public sealed class DemoDataOptions
{
    public bool Enabled { get; init; }
    public bool SeedIfEmpty { get; init; } = true;
}
