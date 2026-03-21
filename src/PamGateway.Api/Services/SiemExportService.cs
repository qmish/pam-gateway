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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SIEM export failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }
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
               $"outcome={evt.Result} rt={evt.Timestamp:o}";
    }
}
