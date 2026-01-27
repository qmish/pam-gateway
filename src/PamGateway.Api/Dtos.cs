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
    string Effect
);

public sealed record ApprovalCreateDto(
    string RequestId,
    string Approver,
    string Status
);

public sealed record AgentRegisterDto(
    string AgentId,
    string Hostname,
    string Os,
    Dictionary<string, string> Labels,
    List<string> Capabilities
);

public sealed record AgentHeartbeatDto(
    string AgentId,
    string Status,
    int ActiveSessions,
    Dictionary<string, string> Labels
);
