using PamGateway.Core;

namespace PamGateway.Data;

public sealed class TargetEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class AccessRequestEntity
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

public sealed class SessionEntity
{
    public string Id { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public SessionStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
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

public sealed class RoleEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class PolicyEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string AllowedProtocols { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
}

public sealed class ApprovalEntity
{
    public string Id { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Approver { get; set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
