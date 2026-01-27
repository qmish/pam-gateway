using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PamGateway.Integrations;

public interface ICmdbClient
{
    Task<IReadOnlyList<CmdbTarget>> FetchTargetsAsync(CancellationToken cancellationToken);
}

public sealed record CmdbTarget(string Id, string Name, string Type, string Environment, string Criticality, string Status);

public sealed class CmdbOptions
{
    public string BaseUrl { get; set; } = "";
    public string Iql { get; set; } = "objectType=System";
    public string AuthType { get; set; } = "Bearer";
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
    public string TypeAttribute { get; set; } = "Тип";
    public string EnvironmentAttribute { get; set; } = "Среда";
    public string CriticalityAttribute { get; set; } = "Критичность";
    public string StatusAttribute { get; set; } = "Статус";
    public string DefaultType { get; set; } = "Unknown";
    public string DefaultEnvironment { get; set; } = "prod";
    public string DefaultCriticality { get; set; } = "non-critical";
    public string DefaultStatus { get; set; } = "Используется";
}

public sealed class JiraInsightClient : ICmdbClient
{
    private readonly HttpClient _httpClient;
    private readonly CmdbOptions _options;

    public JiraInsightClient(HttpClient httpClient, IOptions<CmdbOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<CmdbTarget>> FetchTargetsAsync(CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/rest/insight/1.0/object/navlist/iql?iql={Uri.EscapeDataString(_options.Iql)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var targets = new List<CmdbTarget>();
        if (!doc.RootElement.TryGetProperty("objectEntries", out var entries))
        {
            return targets;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            var id = entry.GetProperty("id").GetInt64().ToString();
            var name = entry.TryGetProperty("label", out var label) ? label.GetString() ?? id : id;
            var attributes = await FetchAttributesAsync(id, cancellationToken);
            var type = GetAttributeValue(attributes, _options.TypeAttribute, _options.DefaultType);
            var environment = GetAttributeValue(attributes, _options.EnvironmentAttribute, _options.DefaultEnvironment);
            var criticality = GetAttributeValue(attributes, _options.CriticalityAttribute, _options.DefaultCriticality);
            var status = GetAttributeValue(attributes, _options.StatusAttribute, _options.DefaultStatus);
            targets.Add(new CmdbTarget(
                id,
                name,
                type,
                environment,
                criticality,
                status));
        }

        return targets;
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

    private async Task<Dictionary<string, string>> FetchAttributesAsync(string objectId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TypeAttribute)
            && string.IsNullOrWhiteSpace(_options.EnvironmentAttribute)
            && string.IsNullOrWhiteSpace(_options.CriticalityAttribute)
            && string.IsNullOrWhiteSpace(_options.StatusAttribute))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/rest/insight/1.0/object/{objectId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyAuth(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!doc.RootElement.TryGetProperty("attributes", out var attributesElement))
        {
            return attributes;
        }

        foreach (var attribute in attributesElement.EnumerateArray())
        {
            if (!attribute.TryGetProperty("objectTypeAttribute", out var typeAttribute))
            {
                continue;
            }

            if (!typeAttribute.TryGetProperty("name", out var nameProperty))
            {
                continue;
            }

            var name = nameProperty.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (TryGetAttributeValue(attribute, out var value))
            {
                attributes[name] = value;
            }
        }

        return attributes;
    }

    private static string GetAttributeValue(
        IReadOnlyDictionary<string, string> attributes,
        string attributeName,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return fallback;
        }

        return attributes.TryGetValue(attributeName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static bool TryGetAttributeValue(JsonElement attribute, out string value)
    {
        value = string.Empty;
        if (!attribute.TryGetProperty("objectAttributeValues", out var values))
        {
            return false;
        }

        foreach (var item in values.EnumerateArray())
        {
            if (TryExtractValue(item, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractValue(JsonElement item, out string value)
    {
        value = string.Empty;

        if (item.TryGetProperty("value", out var rawValue))
        {
            value = rawValue.ValueKind == JsonValueKind.String ? rawValue.GetString() ?? "" : rawValue.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        if (item.TryGetProperty("displayValue", out var displayValue))
        {
            value = displayValue.GetString() ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }

        if (item.TryGetProperty("referencedObject", out var referenced))
        {
            if (referenced.TryGetProperty("label", out var label))
            {
                value = label.GetString() ?? "";
                return !string.IsNullOrWhiteSpace(value);
            }

            if (referenced.TryGetProperty("objectKey", out var objectKey))
            {
                value = objectKey.GetString() ?? "";
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        return false;
    }
}
