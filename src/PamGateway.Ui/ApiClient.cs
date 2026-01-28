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

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken)
        => await _http.GetFromJsonAsync<List<RoleDto>>("/api/v1/roles", cancellationToken) ?? new List<RoleDto>();

    public async Task<IReadOnlyList<SessionDto>?> GetSessionsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync("/api/v1/sessions", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<SessionDto>>(cancellationToken: cancellationToken)
            ?? new List<SessionDto>();
    }

    public async Task<IReadOnlyList<RecordingDto>?> GetRecordingsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync("/api/v1/recordings", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<RecordingDto>>(cancellationToken: cancellationToken)
            ?? new List<RecordingDto>();
    }

    public async Task<RoleDto?> CreateRoleAsync(RoleCreateDto role, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/roles", role, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<RoleDto>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AccessRequestDto>?> GetAccessRequestsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync("/api/v1/access/requests", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<AccessRequestDto>>(cancellationToken: cancellationToken)
            ?? new List<AccessRequestDto>();
    }

    public async Task<AccessRequestDto?> CreateAccessRequestAsync(AccessRequestCreateDto request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/access/requests", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AccessRequestDto>(cancellationToken: cancellationToken);
    }

    public async Task<AccessRequestDto?> ApproveAccessRequestAsync(string id, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync($"/api/v1/access/requests/{Uri.EscapeDataString(id)}/approve", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AccessRequestDto>(cancellationToken: cancellationToken);
    }

    public async Task<AccessRequestDto?> DenyAccessRequestAsync(string id, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync($"/api/v1/access/requests/{Uri.EscapeDataString(id)}/deny", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<AccessRequestDto>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalDto>?> GetApprovalsAsync(CancellationToken cancellationToken)
        => await _http.GetFromJsonAsync<List<ApprovalDto>>("/api/v1/approvals", cancellationToken);

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

public sealed record RoleDto(
    string Id,
    string Name,
    string Description
);

public sealed record RoleCreateDto(
    string Name,
    string Description
);

public sealed record SessionDto(
    string Id,
    string TargetId,
    string RequestId,
    string Protocol,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt
);

public sealed record RecordingDto(
    string Id,
    string SessionId,
    string Mode,
    string? StorageUri,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? SizeBytes,
    string? Hash
);

public sealed record AccessRequestDto(
    string Id,
    string TargetId,
    string RequestedBy,
    int DurationMinutes,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? ItsmKey
);

public sealed record AccessRequestCreateDto(
    string TargetId,
    int DurationMinutes,
    string Reason
);

public sealed record ApprovalDto(
    string Id,
    string RequestId,
    string Approver,
    DateTimeOffset ApprovedAt,
    string Status
);
