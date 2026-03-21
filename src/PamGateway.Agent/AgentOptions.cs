namespace PamGateway.Agent;

public sealed class AgentOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:8080";
    public string ListenUrl { get; set; } = "http://0.0.0.0:7071";
    public string PublicUrl { get; set; } = "http://localhost:7071";
    public string AgentId { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string Os { get; set; } = "";
    public string? JoinToken { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public List<string>? Capabilities { get; set; }
    public bool VerifyTicket { get; set; } = true;
    public int MaxParallelSessions { get; set; } = 50;
    public int IdleTimeoutSeconds { get; set; } = 1800;
    public int KeepaliveIntervalSeconds { get; set; } = 30;
    public string BindAddress { get; set; } = "0.0.0.0";
}
