namespace PamGateway.Api;

public sealed class AgentApiOptions
{
    public string JoinToken { get; init; } = string.Empty;
    public bool RequireAgentToken { get; init; } = true;
}
