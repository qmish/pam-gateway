using PamGateway.Core;

namespace PamGateway.Data;

public abstract class SoftDeletableEntity
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class TargetEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? LabelsJson { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class AccessRequestEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AccessRequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? ItsmKey { get; set; }
}

public sealed class SessionEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public SessionStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public sealed class SessionRecordingEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string? StorageUri { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public long? SizeBytes { get; set; }
    public string? Hash { get; set; }
}

public sealed class AuditEventEntity
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
}

public sealed class RoleEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class PolicyEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string AllowedProtocols { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public string? TargetLabelSelectorJson { get; set; }
}

public sealed class ApprovalEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Approver { get; set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class AgentEntity : SoftDeletableEntity
{
    public string Id { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public string LabelsJson { get; set; } = "{}";
    public string CapabilitiesJson { get; set; } = "[]";
    public string Token { get; set; } = string.Empty;
}

public sealed class AgentTicketEntity
{
    public string Ticket { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
