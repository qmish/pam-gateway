namespace PamGateway.Api;

public sealed class AccessOptions
{
    public Dictionary<string, Dictionary<string, string>> RoleLabelRules { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
}
