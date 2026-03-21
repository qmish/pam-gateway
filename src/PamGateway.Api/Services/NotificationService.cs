using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PamGateway.Api.Services;

public sealed class NotificationOptions
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = "";
    public string? WebhookSecret { get; set; }
    public List<string> Events { get; set; } = new()
    {
        "access.approved",
        "access.denied",
        "access.expired",
        "access.sla_escalation"
    };
}

public interface INotificationService
{
    Task NotifyAsync(string eventType, object payload, CancellationToken cancellationToken = default);
}

public sealed class WebhookNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly NotificationOptions _options;
    private readonly ILogger<WebhookNotificationService> _logger;

    public WebhookNotificationService(
        HttpClient httpClient,
        IOptions<NotificationOptions> options,
        ILogger<WebhookNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyAsync(string eventType, object payload, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.WebhookUrl))
            return;

        if (_options.Events.Count > 0 && !_options.Events.Contains(eventType, StringComparer.OrdinalIgnoreCase))
            return;

        var envelope = new
        {
            event_type = eventType,
            timestamp = DateTimeOffset.UtcNow,
            data = payload
        };

        var json = JsonSerializer.Serialize(envelope);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.WebhookUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
            request.Headers.TryAddWithoutValidation("X-Pam-Webhook-Secret", _options.WebhookSecret);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Notification webhook returned {Status} for {Event}.",
                    (int)response.StatusCode, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification webhook failed for {Event}.", eventType);
        }
    }
}

public sealed class NoopNotificationService : INotificationService
{
    public Task NotifyAsync(string eventType, object payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
