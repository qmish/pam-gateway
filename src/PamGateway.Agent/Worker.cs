using System.Net.Http.Json;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace PamGateway.Agent;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentOptions _options;
    private string? _agentToken;
    private int _heartbeatIntervalSec = 30;
    private int _consecutiveFailures;
    private const int ReconnectThreshold = 3;

    public Worker(
        ILogger<Worker> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AgentOptions> options)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureRegisteredAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatAsync(stoppingToken);
                _consecutiveFailures = 0;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogWarning(ex, "Heartbeat failed ({Failures} consecutive)", _consecutiveFailures);

                if (_consecutiveFailures >= ReconnectThreshold)
                {
                    _logger.LogWarning("Lost connection to API ({Failures} failures), re-registering...", _consecutiveFailures);
                    _agentToken = null;
                    _consecutiveFailures = 0;
                    await EnsureRegisteredAsync(stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_heartbeatIntervalSec), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent shutting down gracefully...");

        try
        {
            var http = _httpClientFactory.CreateClient("PamGateway");
            if (!string.IsNullOrWhiteSpace(_agentToken))
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _agentToken);
            }

            var agentId = string.IsNullOrWhiteSpace(_options.AgentId) ? Environment.MachineName : _options.AgentId;
            var payload = new AgentHeartbeatRequest
            {
                AgentId = agentId,
                Status = "offline",
                ActiveSessions = 0,
                Labels = _options.Labels ?? new Dictionary<string, string>()
            };

            await http.PostAsJsonAsync("/api/v1/agents/heartbeat", payload, cancellationToken);
            _logger.LogInformation("Sent offline status to API before shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send offline status during shutdown");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task EnsureRegisteredAsync(CancellationToken stoppingToken)
    {
        var http = _httpClientFactory.CreateClient("PamGateway");
        while (!stoppingToken.IsCancellationRequested)
        {
            var payload = new AgentRegisterRequest
            {
                JoinToken = _options.JoinToken,
                AgentId = string.IsNullOrWhiteSpace(_options.AgentId) ? Environment.MachineName : _options.AgentId,
                Hostname = string.IsNullOrWhiteSpace(_options.Hostname) ? Environment.MachineName : _options.Hostname,
                Os = string.IsNullOrWhiteSpace(_options.Os) ? RuntimeInformation.OSDescription : _options.Os,
                PublicUrl = _options.PublicUrl,
                Labels = _options.Labels ?? new Dictionary<string, string>(),
                Capabilities = _options.Capabilities ?? new List<string>()
            };

            try
            {
                var response = await http.PostAsJsonAsync("/api/v1/agents/register", payload, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Register failed: {StatusCode}", response.StatusCode);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var data = await response.Content.ReadFromJsonAsync<AgentRegisterResponse>(cancellationToken: stoppingToken);
                if (data is null || string.IsNullOrWhiteSpace(data.AgentToken))
                {
                    _logger.LogWarning("Register response missing agent token");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                _agentToken = data.AgentToken;
                _heartbeatIntervalSec = data.HeartbeatIntervalSec > 0 ? data.HeartbeatIntervalSec : _heartbeatIntervalSec;
                _logger.LogInformation("Agent registered: {AgentId}", payload.AgentId);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Register exception");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken stoppingToken)
    {
        var http = _httpClientFactory.CreateClient("PamGateway");
        if (!string.IsNullOrWhiteSpace(_agentToken))
        {
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _agentToken);
        }

        var payload = new AgentHeartbeatRequest
        {
            AgentId = string.IsNullOrWhiteSpace(_options.AgentId) ? Environment.MachineName : _options.AgentId,
            Status = "online",
            ActiveSessions = 0,
            Labels = _options.Labels ?? new Dictionary<string, string>()
        };

        var response = await http.PostAsJsonAsync("/api/v1/agents/heartbeat", payload, stoppingToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Heartbeat status: {StatusCode}", response.StatusCode);
            throw new HttpRequestException($"Heartbeat failed with {response.StatusCode}");
        }
    }
}

public sealed class AgentRegisterRequest
{
    public string? JoinToken { get; init; }
    public string AgentId { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string Os { get; init; } = string.Empty;
    public string? PublicUrl { get; init; }
    public Dictionary<string, string> Labels { get; init; } = new();
    public List<string> Capabilities { get; init; } = new();
}

public sealed class AgentRegisterResponse
{
    public string AgentToken { get; init; } = string.Empty;
    public string AgentCert { get; init; } = string.Empty;
    public int HeartbeatIntervalSec { get; init; }
}

public sealed class AgentHeartbeatRequest
{
    public string AgentId { get; init; } = string.Empty;
    public string Status { get; init; } = "online";
    public int ActiveSessions { get; init; }
    public Dictionary<string, string> Labels { get; init; } = new();
}
