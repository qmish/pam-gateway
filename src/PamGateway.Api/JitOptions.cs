namespace PamGateway.Api;

public sealed class JitOptions
{
    public int MaxActiveRequestsPerUser { get; set; } = 5;
}
