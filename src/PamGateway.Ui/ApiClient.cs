using System.Net.Http.Json;

namespace PamGateway.Ui;

public sealed class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<TargetDto>> GetTargetsAsync(CancellationToken cancellationToken)
        => await _http.GetFromJsonAsync<List<TargetDto>>("/api/v1/targets", cancellationToken) ?? new List<TargetDto>();

    public async Task<IReadOnlyList<AgentDto>> GetAgentsAsync(CancellationToken cancellationToken)
        => await _http.GetFromJsonAsync<List<AgentDto>>("/api/v1/agents", cancellationToken) ?? new List<AgentDto>();

    public async Task<IReadOnlyList<PolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken)
        => await _http.GetFromJsonAsync<List<PolicyDto>>("/api/v1/policies", cancellationToken) ?? new List<PolicyDto>();

    public async Task<TargetDto?> CreateTargetAsync(TargetUpsertDto target, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/targets", target, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TargetDto>(cancellationToken: cancellationToken);
    }

    public async Task<TargetDto?> UpdateTargetAsync(TargetUpsertDto target, CancellationToken cancellationToken)
    {
        var response = await _http.PutAsJsonAsync($"/api/v1/targets/{Uri.EscapeDataString(target.Id)}", target, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TargetDto>(cancellationToken: cancellationToken);
    }

    public async Task<PolicyDto?> CreatePolicyAsync(PolicyCreateDto policy, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/policies", policy, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PolicyDto>(cancellationToken: cancellationToken);
    }

    public async Task<PolicyDto?> UpdatePolicyAsync(PolicyUpsertDto policy, CancellationToken cancellationToken)
    {
        var response = await _http.PutAsJsonAsync($"/api/v1/policies/{Uri.EscapeDataString(policy.Id)}", policy, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PolicyDto>(cancellationToken: cancellationToken);
    }
}

public sealed record TargetDto(
    string Id,
    string Name,
    string? Host,
    int? Port,
    Dictionary<string, string>? Labels,
    string Type,
    string Environment,
    string Criticality,
    string Status
);

public sealed record TargetUpsertDto(
    string Id,
    string Name,
    string? Host,
    int? Port,
    Dictionary<string, string>? Labels,
    string Type,
    string Environment,
    string Criticality,
    string Status
);

public sealed record AgentDto(
    string Id,
    string Hostname,
    string Os,
    string Status,
    DateTimeOffset LastSeenAt,
    string PublicUrl,
    Dictionary<string, string> Labels,
    List<string> Capabilities
);

public sealed record PolicyDto(
    string Id,
    string Name,
    string TargetType,
    string AllowedProtocols,
    string Effect,
    Dictionary<string, string>? TargetLabelSelector
);

public sealed record PolicyCreateDto(
    string Name,
    string TargetType,
    string AllowedProtocols,
    string Effect,
    Dictionary<string, string>? TargetLabelSelector
);

public sealed record PolicyUpsertDto(
    string Id,
    string Name,
    string TargetType,
    string AllowedProtocols,
    string Effect,
    Dictionary<string, string>? TargetLabelSelector
);
