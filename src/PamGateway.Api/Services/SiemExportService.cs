using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PamGateway.Core;

namespace PamGateway.Api.Services;

public sealed class SiemExportOptions
{
    public bool Enabled { get; set; }
    public string Transport { get; set; } = "webhook";
    public string? WebhookUrl { get; set; }
    public string? SyslogHost { get; set; }
    public int SyslogPort { get; set; } = 514;
    public int IntervalSeconds { get; set; } = 30;
    public bool HeartbeatEnabled { get; set; } = true;
    public int HeartbeatIntervalSeconds { get; set; } = 300;
}

public static class SiemEventTypes
{
    public const string UserLogin = "user.login";
    public const string UserLogout = "user.logout";
    public const string AccessRequested = "access.requested";
    public const string AccessApproved = "access.approved";
    public const string AccessDenied = "access.denied";
    public const string AccessExpired = "access.expired";
    public const string SessionStarted = "session.started";
    public const string SessionEnded = "session.ended";
    public const string PolicyViolation = "policy.violation";
    public const string AgentOnline = "agent.online";
    public const string AgentOffline = "agent.offline";
    public const string VaultCheckout = "vault.credential.checkout";
    public const string VaultCheckin = "vault.credential.checkin";
    public const string VaultRotated = "vault.credential.rotated";
    public const string BreakGlassCheckout = "vault.breakglass.checkout";
    public const string SystemHeartbeat = "system.heartbeat";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UserLogin, UserLogout, AccessRequested, AccessApproved, AccessDenied,
        AccessExpired, SessionStarted, SessionEnded, PolicyViolation,
        AgentOnline, AgentOffline, VaultCheckout, VaultCheckin, VaultRotated,
        BreakGlassCheckout, SystemHeartbeat
    };
}

public sealed class SiemExportService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SiemExportOptions _options;
    private readonly ILogger<SiemExportService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private int _lastExportedIndex;

    public SiemExportService(
        IServiceProvider serviceProvider,
        IOptions<SiemExportOptions> options,
        ILogger<SiemExportService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private DateTimeOffset _lastHeartbeat = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SIEM export is disabled");
            return;
        }

        _logger.LogInformation("SIEM export started: transport={Transport}", _options.Transport);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExportNewEventsAsync(stoppingToken);
                await SendHeartbeatIfDueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SIEM export failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }
    }

    private async Task SendHeartbeatIfDueAsync(CancellationToken cancellationToken)
    {
        if (!_options.HeartbeatEnabled) return;
        if ((DateTimeOffset.UtcNow - _lastHeartbeat).TotalSeconds < _options.HeartbeatIntervalSeconds) return;

        var heartbeat = new AuditEvent(
            DateTimeOffset.UtcNow,
            SiemEventTypes.SystemHeartbeat,
            "system", "PAM Gateway", "system",
            "", "", "heartbeat", "ok", "", "", "127.0.0.1"
        );

        if (_options.Transport.Equals("syslog", StringComparison.OrdinalIgnoreCase))
            await ExportViaSyslogAsync(new[] { heartbeat }, cancellationToken);
        else
            await ExportViaWebhookAsync(new[] { heartbeat }, cancellationToken);

        _lastHeartbeat = DateTimeOffset.UtcNow;
        _logger.LogDebug("SIEM heartbeat sent");
    }

    private async Task ExportNewEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var auditStore = scope.ServiceProvider.GetRequiredService<IAuditStore>();
        var allEvents = auditStore.GetAll();

        if (allEvents.Count <= _lastExportedIndex)
        {
            return;
        }

        var newEvents = allEvents.Skip(_lastExportedIndex).ToList();
        _logger.LogDebug("Exporting {Count} audit events to SIEM", newEvents.Count);

        if (_options.Transport.Equals("syslog", StringComparison.OrdinalIgnoreCase))
        {
            await ExportViaSyslogAsync(newEvents, cancellationToken);
        }
        else
        {
            await ExportViaWebhookAsync(newEvents, cancellationToken);
        }

        _lastExportedIndex = allEvents.Count;
    }

    private async Task ExportViaWebhookAsync(IReadOnlyList<AuditEvent> events, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            _logger.LogWarning("SIEM webhook URL not configured");
            return;
        }

        var client = _httpClientFactory.CreateClient("SiemWebhook");
        var payload = JsonSerializer.Serialize(events, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(_options.WebhookUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SIEM webhook returned {StatusCode}", response.StatusCode);
        }
        else
        {
            _logger.LogDebug("Exported {Count} events via webhook", events.Count);
        }
    }

    private async Task ExportViaSyslogAsync(IReadOnlyList<AuditEvent> events, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SyslogHost))
        {
            _logger.LogWarning("SIEM syslog host not configured");
            return;
        }

        using var udpClient = new UdpClient();
        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = FormatCefMessage(evt);
            var bytes = Encoding.UTF8.GetBytes(message);
            await udpClient.SendAsync(bytes, bytes.Length, _options.SyslogHost, _options.SyslogPort);
        }

        _logger.LogDebug("Exported {Count} events via syslog to {Host}:{Port}", events.Count, _options.SyslogHost, _options.SyslogPort);
    }

    public static string FormatCefMessage(AuditEvent evt)
    {
        var severity = evt.Result == "denied" || evt.Result == "failure" ? "7" : "3";
        return $"CEF:0|PAMGateway|PAM|1.0|{evt.EventType}|{evt.Action}|{severity}|" +
               $"src={evt.SourceIp} suser={evt.Username} duser={evt.UserId} " +
               $"dst={evt.TargetId} dhost={evt.TargetName} " +
               $"cs1={evt.RequestId} cs2={evt.SessionId} " +
               $"requestClientApplication={EscapeCef(evt.UserAgent)} " +
               $"outcome={evt.Result} rt={evt.Timestamp:o}";
    }

    private static string EscapeCef(string value)
        => value.Replace("\\", "\\\\").Replace("=", "\\=").Replace("|", "\\|");
}
