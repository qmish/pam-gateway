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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_heartbeatIntervalSec), stoppingToken);
        }
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
        }
    }
}

public sealed class AgentRegisterRequest
{
    public string? JoinToken { get; init; }
    public string AgentId { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string Os { get; init; } = string.Empty;
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
