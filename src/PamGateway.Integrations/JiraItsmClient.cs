using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PamGateway.Integrations;

public interface IItsmClient
{
    Task<ItsmTicket> CreateAccessRequestAsync(ItsmAccessRequest request, CancellationToken cancellationToken);
    Task UpdateStatusAsync(string ticketKey, string status, CancellationToken cancellationToken);
}

public sealed record ItsmAccessRequest(string Summary, string Description, string RequestedBy, string TargetId, string DurationMinutes);
public sealed record ItsmTicket(string Key, string Url);

public sealed class JiraOptions
{
    public string BaseUrl { get; set; } = "";
    public string ProjectKey { get; set; } = "";
    public string IssueType { get; set; } = "Task";
    public string AuthType { get; set; } = "Bearer";
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public Dictionary<string, string> StatusMap { get; set; } = new();
    public string TransitionPending { get; set; } = "";
    public string TransitionApproved { get; set; } = "";
    public string TransitionDenied { get; set; } = "";
    public string TransitionExpired { get; set; } = "";
    public Dictionary<string, string> TransitionMap { get; set; } = new();
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
}

public sealed class JiraItsmClient : IItsmClient
{
    private readonly HttpClient _httpClient;
    private readonly JiraOptions _options;

    public JiraItsmClient(HttpClient httpClient, IOptions<JiraOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ItsmTicket> CreateAccessRequestAsync(ItsmAccessRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/rest/api/2/issue");
        ApplyAuth(httpRequest);

        var payload = new
        {
            fields = new
            {
                project = new { key = _options.ProjectKey },
                summary = request.Summary,
                description = request.Description,
                issuetype = new { name = _options.IssueType }
            }
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var key = doc.RootElement.GetProperty("key").GetString() ?? "";

        return new ItsmTicket(key, $"{_options.BaseUrl.TrimEnd('/')}/browse/{key}");
    }

    public async Task UpdateStatusAsync(string ticketKey, string status, CancellationToken cancellationToken)
    {
        var transitionId = ResolveTransitionId(status);

        if (string.IsNullOrWhiteSpace(transitionId))
        {
            return;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/rest/api/2/issue/{ticketKey}/transitions");
        ApplyAuth(httpRequest);

        var payload = new
        {
            transition = new { id = transitionId }
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private string ResolveTransitionId(string status)
    {
        if (_options.TransitionMap.TryGetValue(status, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            return mapped;

        return status switch
        {
            "pending" => _options.TransitionPending,
            "approved" => _options.TransitionApproved,
            "denied" => _options.TransitionDenied,
            "expired" => _options.TransitionExpired,
            _ => ""
        };
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (_options.AuthType.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            var raw = $"{_options.Username}:{_options.Token}";
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
    }
}
