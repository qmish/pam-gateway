using System.ComponentModel.DataAnnotations;

namespace PamGateway.Api;

public sealed record AccessRequestCreateDto(
    [Required, MinLength(1)] string TargetId,
    [Range(1, 1440)] int DurationMinutes,
    [Required, MinLength(1), MaxLength(1000)] string Reason
);

public sealed record SessionCreateDto(
    [Required, MinLength(1)] string TargetId,
    [Required, MinLength(1)] string Protocol,
    [Required, MinLength(1)] string RequestId
);

public sealed record RoleCreateDto(
    [Required, MinLength(1), MaxLength(200)] string Name,
    [MaxLength(1000)] string Description
);

public sealed record PolicyCreateDto(
    [Required, MinLength(1), MaxLength(200)] string Name,
    [Required, MinLength(1)] string TargetType,
    [Required, MinLength(1)] string AllowedProtocols,
    [Required, RegularExpression("^(?i)(allow|deny)$", ErrorMessage = "Effect must be 'Allow' or 'Deny'.")] string Effect,
    Dictionary<string, string>? TargetLabelSelector
);

public sealed record PolicyUpsertDto(
    [Required, MinLength(1)] string Id,
    [Required, MinLength(1), MaxLength(200)] string Name,
    [Required, MinLength(1)] string TargetType,
    [Required, MinLength(1)] string AllowedProtocols,
    [Required, RegularExpression("^(?i)(allow|deny)$", ErrorMessage = "Effect must be 'Allow' or 'Deny'.")] string Effect,
    Dictionary<string, string>? TargetLabelSelector
);

public sealed record ApprovalCreateDto(
    [Required, MinLength(1)] string RequestId,
    [Required, MinLength(1)] string Approver,
    [Required, RegularExpression("^(approved|denied)$", ErrorMessage = "Status must be 'approved' or 'denied'.")] string Status
);

public sealed record AgentRegisterDto(
    string? JoinToken,
    [Required, MinLength(1)] string AgentId,
    [Required, MinLength(1)] string Hostname,
    [Required, MinLength(1)] string Os,
    string? PublicUrl,
    Dictionary<string, string> Labels,
    List<string> Capabilities
);

public sealed record AgentHeartbeatDto(
    [Required, MinLength(1)] string AgentId,
    [Required, MinLength(1)] string Status,
    [Range(0, 10000)] int ActiveSessions,
    Dictionary<string, string> Labels
);

public sealed record SessionTicketIssueDto(
    [Required, MinLength(1)] string AgentId,
    [Range(30, 3600)] int? ExpiresInSeconds
);

public sealed record AgentSessionCreateDto(
    [Required, MinLength(1)] string SessionId,
    [Required, MinLength(1)] string TargetId,
    [Required, MinLength(1)] string Protocol,
    [Required, MinLength(1)] string User,
    [Required, MinLength(1)] string Ticket,
    DateTimeOffset ExpiresAt
);

public sealed record AgentSessionTerminateDto(
    [Required, MinLength(1)] string Reason
);

public sealed record TargetUpsertDto(
    [Required, MinLength(1)] string Id,
    [Required, MinLength(1), MaxLength(500)] string Name,
    string? Host,
    [Range(1, 65535)] int? Port,
    Dictionary<string, string>? Labels,
    [Required, MinLength(1)] string Type,
    [Required, MinLength(1)] string Environment,
    [Required, MinLength(1)] string Criticality,
    [Required, MinLength(1)] string Status
);

public sealed record RecordingCreateDto(
    [Required, MinLength(1)] string SessionId,
    [Required, RegularExpression("^(node|node-sync|proxy|proxy-sync)$", ErrorMessage = "Invalid recording mode.")] string Mode,
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
