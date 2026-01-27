namespace PamGateway.Core;

public enum AccessRequestStatus
{
    Pending,
    Approved,
    Denied,
    Expired
}

public enum SessionStatus
{
    Pending,
    Active,
    Terminated
}

public enum AgentStatus
{
    Pending,
    Online,
    Offline
}

public sealed record TargetSystem(
    string Id,
    string Name,
    string? Host,
    int? Port,
    string Type,
    string Environment,
    string Criticality,
    string Status
);

public sealed record AccessRequest(
    string Id,
    string TargetId,
    string RequestedBy,
    int DurationMinutes,
    string Reason,
    AccessRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? ItsmKey
);

public sealed record Session(
    string Id,
    string TargetId,
    string RequestId,
    string Protocol,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt
);

public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string EventType,
    string UserId,
    string Username,
    string Role,
    string TargetId,
    string TargetName,
    string Action,
    string Result,
    string RequestId,
    string SessionId,
    string SourceIp
);

public sealed record Role(
    string Id,
    string Name,
    string Description
);

public sealed record Policy(
    string Id,
    string Name,
    string TargetType,
    string AllowedProtocols,
    string Effect
);

public sealed record Approval(
    string Id,
    string RequestId,
    string Approver,
    DateTimeOffset ApprovedAt,
    string Status
);

public sealed record AgentInfo(
    string Id,
    string Hostname,
    string Os,
    AgentStatus Status,
    DateTimeOffset LastSeenAt,
    string PublicUrl,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<string> Capabilities,
    string Token
);

public sealed record AgentSessionTicket(
    string Ticket,
    string SessionId,
    string AgentId,
    DateTimeOffset ExpiresAt
);

public interface IAccessRequestStore
{
    IReadOnlyList<AccessRequest> GetAll();
    AccessRequest? GetById(string id);
    AccessRequest Add(AccessRequest request);
    AccessRequest Update(AccessRequest request);
}

public interface ISessionStore
{
    IReadOnlyList<Session> GetAll();
    Session? GetById(string id);
    Session Add(Session session);
    Session Update(Session session);
}

public interface ITargetStore
{
    IReadOnlyList<TargetSystem> GetAll();
    TargetSystem? GetById(string id);
    void AddOrUpdate(TargetSystem target);
    void AddOrUpdateRange(IEnumerable<TargetSystem> targets);
}

public interface IAuditStore
{
    IReadOnlyList<AuditEvent> GetAll();
    void Add(AuditEvent auditEvent);
}

public interface IRoleStore
{
    IReadOnlyList<Role> GetAll();
    Role? GetById(string id);
    Role Add(Role role);
}

public interface IPolicyStore
{
    IReadOnlyList<Policy> GetAll();
    Policy? GetById(string id);
    Policy Add(Policy policy);
}

public interface IApprovalStore
{
    IReadOnlyList<Approval> GetAll();
    Approval Add(Approval approval);
}

public interface IAgentStore
{
    IReadOnlyList<AgentInfo> GetAll();
    AgentInfo? GetById(string id);
    AgentInfo Register(AgentInfo agent);
    AgentInfo UpdateHeartbeat(string id, DateTimeOffset lastSeenAt, AgentStatus status);
}

public interface IAgentTicketStore
{
    AgentSessionTicket Issue(string sessionId, string agentId, DateTimeOffset expiresAt);
    AgentSessionTicket? GetByTicket(string ticket);
    void Revoke(string ticket);
}
