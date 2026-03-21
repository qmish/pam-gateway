using PamGateway.Core;

namespace PamGateway.Api.Services;

public sealed class AgentHealthMonitorOptions
{
    public int CheckIntervalSeconds { get; set; } = 60;
    public int OfflineThresholdSeconds { get; set; } = 90;
}

public sealed class AgentHealthMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentHealthMonitorOptions _options;
    private readonly ILogger<AgentHealthMonitorService> _logger;

    public AgentHealthMonitorService(
        IServiceProvider serviceProvider,
        Microsoft.Extensions.Options.IOptions<AgentHealthMonitorOptions> options,
        ILogger<AgentHealthMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent health monitor started, interval={Interval}s, threshold={Threshold}s",
            _options.CheckIntervalSeconds, _options.OfflineThresholdSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CheckAgentHealth();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent health check failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.CheckIntervalSeconds), stoppingToken);
        }
    }

    public void CheckAgentHealth()
    {
        using var scope = _serviceProvider.CreateScope();
        var agents = scope.ServiceProvider.GetRequiredService<IAgentStore>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditStore>();

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.OfflineThresholdSeconds);

        foreach (var agent in agents.GetAll())
        {
            if (agent.Status == AgentStatus.Online && agent.LastSeenAt < cutoff)
            {
                agents.UpdateHeartbeat(agent.Id, agent.LastSeenAt, AgentStatus.Offline);

                audit.Add(new AuditEvent(
                    DateTimeOffset.UtcNow, "agent.offline", "system", "system", "system",
                    "", agent.Hostname, "status_change", "offline",
                    "", "", $"last_seen={agent.LastSeenAt:o}"));

                _logger.LogWarning("Agent {AgentId} ({Hostname}) marked Offline — last seen {LastSeen}",
                    agent.Id, agent.Hostname, agent.LastSeenAt);
            }
        }
    }
}
