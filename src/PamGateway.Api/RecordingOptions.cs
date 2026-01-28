namespace PamGateway.Api;

public sealed class RecordingOptions
{
    public string DefaultMode { get; init; } = "node";
    public List<string> AllowedModes { get; init; } = new() { "node", "node-sync", "proxy", "proxy-sync" };
}
