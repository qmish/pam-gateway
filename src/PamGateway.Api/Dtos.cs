namespace PamGateway.Api;

public sealed record AccessRequestCreateDto(
    string TargetId,
    int DurationMinutes,
    string Reason
);

public sealed record SessionCreateDto(
    string TargetId,
    string Protocol,
    string RequestId
);

public sealed record RoleCreateDto(
    string Name,
    string Description
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

public sealed record ApprovalCreateDto(
    string RequestId,
    string Approver,
    string Status
);

public sealed record AgentRegisterDto(
    string? JoinToken,
    string AgentId,
    string Hostname,
    string Os,
    string? PublicUrl,
    Dictionary<string, string> Labels,
    List<string> Capabilities
);

public sealed record AgentHeartbeatDto(
    string AgentId,
    string Status,
    int ActiveSessions,
    Dictionary<string, string> Labels
);

public sealed record SessionTicketIssueDto(
    string AgentId,
    int? ExpiresInSeconds
);

public sealed record AgentSessionCreateDto(
    string SessionId,
    string TargetId,
    string Protocol,
    string User,
    string Ticket,
    DateTimeOffset ExpiresAt
);

public sealed record AgentSessionTerminateDto(
    string Reason
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

public sealed record RecordingCreateDto(
    string SessionId,
    string Mode,
    string? StorageUri
);

public sealed record RecordingUpdateDto(
    string Id,
    string Status,
    DateTimeOffset? EndedAt,
    long? SizeBytes,
    string? Hash,
    string? StorageUri
);

public sealed record JiraIssueWebhookDto(
    string? IssueEventTypeName,
    JiraIssueDto? Issue
);

public sealed record JiraIssueDto(
    string? Id,
    string? Key,
    JiraIssueFieldsDto? Fields
);

public sealed record JiraIssueFieldsDto(
    JiraStatusDto? Status
);

public sealed record JiraStatusDto(
    string? Name
);
