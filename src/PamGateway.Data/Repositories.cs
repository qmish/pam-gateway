using Microsoft.EntityFrameworkCore;
using PamGateway.Core;

namespace PamGateway.Data;

public sealed class EfAccessRequestStore : IAccessRequestStore
{
    private readonly PamGatewayDbContext _db;

    public EfAccessRequestStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<AccessRequest> GetAll() =>
        _db.AccessRequests.AsNoTracking().Select(Map).ToList();

    public AccessRequest? GetById(string id)
    {
        var entity = _db.AccessRequests.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return entity is null ? null : Map(entity);
    }

    public AccessRequest? GetByItsmKey(string itsmKey)
    {
        var entity = _db.AccessRequests.AsNoTracking()
            .FirstOrDefault(item => item.ItsmKey != null && item.ItsmKey == itsmKey);
        return entity is null ? null : Map(entity);
    }

    public AccessRequest Add(AccessRequest request)
    {
        var entity = Map(request);
        _db.AccessRequests.Add(entity);
        _db.SaveChanges();
        return request;
    }

    public AccessRequest Update(AccessRequest request)
    {
        var entity = Map(request);
        _db.AccessRequests.Update(entity);
        _db.SaveChanges();
        return request;
    }

    private static AccessRequest Map(AccessRequestEntity entity) =>
        new(
            entity.Id,
            entity.TargetId,
            entity.RequestedBy,
            entity.DurationMinutes,
            entity.Reason,
            entity.Status,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.ItsmKey
        );

    private static AccessRequestEntity Map(AccessRequest request) =>
        new()
        {
            Id = request.Id,
            TargetId = request.TargetId,
            RequestedBy = request.RequestedBy,
            DurationMinutes = request.DurationMinutes,
            Reason = request.Reason,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            ExpiresAt = request.ExpiresAt,
            ItsmKey = request.ItsmKey
        };
}

public sealed class EfSessionStore : ISessionStore
{
    private readonly PamGatewayDbContext _db;

    public EfSessionStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Session> GetAll() =>
        _db.Sessions.AsNoTracking().Select(Map).ToList();

    public Session? GetById(string id)
    {
        var entity = _db.Sessions.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return entity is null ? null : Map(entity);
    }

    public Session Add(Session session)
    {
        _db.Sessions.Add(Map(session));
        _db.SaveChanges();
        return session;
    }

    public Session Update(Session session)
    {
        _db.Sessions.Update(Map(session));
        _db.SaveChanges();
        return session;
    }

    private static Session Map(SessionEntity entity) =>
        new(entity.Id, entity.TargetId, entity.RequestId, entity.Protocol, entity.Status, entity.StartedAt, entity.EndedAt);

    private static SessionEntity Map(Session session) =>
        new()
        {
            Id = session.Id,
            TargetId = session.TargetId,
            RequestId = session.RequestId,
            Protocol = session.Protocol,
            Status = session.Status,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt
        };
}

public sealed class EfRecordingStore : IRecordingStore
{
    private readonly PamGatewayDbContext _db;

    public EfRecordingStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<SessionRecording> GetAll() =>
        _db.SessionRecordings.AsNoTracking().Select(Map).ToList();

    public SessionRecording? GetById(string id)
    {
        var entity = _db.SessionRecordings.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return entity is null ? null : Map(entity);
    }

    public SessionRecording Add(SessionRecording recording)
    {
        _db.SessionRecordings.Add(Map(recording));
        _db.SaveChanges();
        return recording;
    }

    public SessionRecording Update(SessionRecording recording)
    {
        _db.SessionRecordings.Update(Map(recording));
        _db.SaveChanges();
        return recording;
    }

    private static SessionRecording Map(SessionRecordingEntity entity)
        => new(
            entity.Id,
            entity.SessionId,
            entity.Mode,
            entity.StorageUri,
            ParseStatus(entity.Status),
            entity.StartedAt,
            entity.EndedAt,
            entity.SizeBytes,
            entity.Hash);

    private static SessionRecordingEntity Map(SessionRecording recording)
        => new()
        {
            Id = recording.Id,
            SessionId = recording.SessionId,
            Mode = recording.Mode,
            StorageUri = recording.StorageUri,
            Status = recording.Status.ToString(),
            StartedAt = recording.StartedAt,
            EndedAt = recording.EndedAt,
            SizeBytes = recording.SizeBytes,
            Hash = recording.Hash
        };

    private static RecordingStatus ParseStatus(string status)
        => Enum.TryParse<RecordingStatus>(status, true, out var parsed)
            ? parsed
            : RecordingStatus.Recording;
}

public sealed class EfTargetStore : ITargetStore
{
    private readonly PamGatewayDbContext _db;

    public EfTargetStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<TargetSystem> GetAll() =>
        _db.Targets.AsNoTracking().Select(Map).ToList();

    public TargetSystem? GetById(string id)
    {
        var entity = _db.Targets.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return entity is null ? null : Map(entity);
    }

    public void AddOrUpdate(TargetSystem target)
    {
        var entity = _db.Targets.FirstOrDefault(item => item.Id == target.Id);
        if (entity is null)
        {
            _db.Targets.Add(Map(target));
        }
        else
        {
            entity.Name = target.Name;
            entity.Type = target.Type;
            entity.Environment = target.Environment;
            entity.Criticality = target.Criticality;
            entity.Status = target.Status;
        }

        _db.SaveChanges();
    }

    public void AddOrUpdateRange(IEnumerable<TargetSystem> targets)
    {
        foreach (var target in targets)
        {
            AddOrUpdate(target);
        }
    }

    private static TargetSystem Map(TargetEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Host,
            entity.Port,
            DeserializeLabels(entity.LabelsJson),
            entity.Type,
            entity.Environment,
            entity.Criticality,
            entity.Status);

    private static TargetEntity Map(TargetSystem target) =>
        new()
        {
            Id = target.Id,
            Name = target.Name,
            Host = target.Host,
            Port = target.Port,
            LabelsJson = SerializeLabels(target.Labels),
            Type = target.Type,
            Environment = target.Environment,
            Criticality = target.Criticality,
            Status = target.Status
        };

    private static IReadOnlyDictionary<string, string>? DeserializeLabels(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

    private static string? SerializeLabels(IReadOnlyDictionary<string, string>? labels)
        => labels is null ? null : System.Text.Json.JsonSerializer.Serialize(labels);
}

public sealed class EfAuditStore : IAuditStore
{
    private readonly PamGatewayDbContext _db;

    public EfAuditStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<AuditEvent> GetAll() =>
        _db.AuditEvents.AsNoTracking().Select(Map).ToList();

    public void Add(AuditEvent auditEvent)
    {
        _db.AuditEvents.Add(Map(auditEvent));
        _db.SaveChanges();
    }

    private static AuditEvent Map(AuditEventEntity entity) =>
        new(
            entity.Timestamp,
            entity.EventType,
            entity.UserId,
            entity.Username,
            entity.Role,
            entity.TargetId,
            entity.TargetName,
            entity.Action,
            entity.Result,
            entity.RequestId,
            entity.SessionId,
            entity.SourceIp
        );

    private static AuditEventEntity Map(AuditEvent auditEvent) =>
        new()
        {
            Timestamp = auditEvent.Timestamp,
            EventType = auditEvent.EventType,
            UserId = auditEvent.UserId,
            Username = auditEvent.Username,
            Role = auditEvent.Role,
            TargetId = auditEvent.TargetId,
            TargetName = auditEvent.TargetName,
            Action = auditEvent.Action,
            Result = auditEvent.Result,
            RequestId = auditEvent.RequestId,
            SessionId = auditEvent.SessionId,
            SourceIp = auditEvent.SourceIp
        };
}

public sealed class EfRoleStore : IRoleStore
{
    private readonly PamGatewayDbContext _db;

    public EfRoleStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Role> GetAll() => _db.Roles.AsNoTracking().Select(Map).ToList();

    public Role? GetById(string id)
    {
        var entity = _db.Roles.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return entity is null ? null : Map(entity);
    }

    public Role Add(Role role)
    {
        _db.Roles.Add(Map(role));
        _db.SaveChanges();
        return role;
    }

    private static Role Map(RoleEntity entity) => new(entity.Id, entity.Name, entity.Description);

    private static RoleEntity Map(Role role) =>
        new() { Id = role.Id, Name = role.Name, Description = role.Description };
}

public sealed class EfPolicyStore : IPolicyStore
{
    private readonly PamGatewayDbContext _db;

    public EfPolicyStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Policy> GetAll() => _db.Policies.AsNoTracking().Select(Map).ToList();

    public Policy? GetById(string id)
    {
        var entity = _db.Policies.AsNoTracking().FirstOrDefault(item => item.Id == id);
        return entity is null ? null : Map(entity);
    }

    public Policy Add(Policy policy)
    {
        _db.Policies.Add(Map(policy));
        _db.SaveChanges();
        return policy;
    }

    public Policy Update(Policy policy)
    {
        var entity = _db.Policies.FirstOrDefault(item => item.Id == policy.Id);
        if (entity is null)
        {
            _db.Policies.Add(Map(policy));
            _db.SaveChanges();
            return policy;
        }

        entity.Name = policy.Name;
        entity.TargetType = policy.TargetType;
        entity.AllowedProtocols = policy.AllowedProtocols;
        entity.Effect = policy.Effect;
        entity.TargetLabelSelectorJson = SerializeSelector(policy.TargetLabelSelector);
        _db.SaveChanges();
        return policy;
    }

    private static Policy Map(PolicyEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.TargetType,
            entity.AllowedProtocols,
            entity.Effect,
            DeserializeSelector(entity.TargetLabelSelectorJson));

    private static PolicyEntity Map(Policy policy) =>
        new()
        {
            Id = policy.Id,
            Name = policy.Name,
            TargetType = policy.TargetType,
            AllowedProtocols = policy.AllowedProtocols,
            Effect = policy.Effect,
            TargetLabelSelectorJson = SerializeSelector(policy.TargetLabelSelector)
        };

    private static IReadOnlyDictionary<string, string>? DeserializeSelector(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

    private static string? SerializeSelector(IReadOnlyDictionary<string, string>? selector)
        => selector is null ? null : System.Text.Json.JsonSerializer.Serialize(selector);
}

public sealed class EfApprovalStore : IApprovalStore
{
    private readonly PamGatewayDbContext _db;

    public EfApprovalStore(PamGatewayDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Approval> GetAll() => _db.Approvals.AsNoTracking().Select(Map).ToList();

    public Approval Add(Approval approval)
    {
        _db.Approvals.Add(Map(approval));
        _db.SaveChanges();
        return approval;
    }

    private static Approval Map(ApprovalEntity entity) =>
        new(entity.Id, entity.RequestId, entity.Approver, entity.ApprovedAt, entity.Status);

    private static ApprovalEntity Map(Approval approval) =>
        new()
        {
            Id = approval.Id,
            RequestId = approval.RequestId,
            Approver = approval.Approver,
            ApprovedAt = approval.ApprovedAt,
            Status = approval.Status
        };
}

public sealed class EfAgentStore : IAgentStore
{
    private readonly PamGatewayDbContext _db;

    public EfAgentStore(PamGatewayDbContext db) => _db = db;

    public IReadOnlyList<AgentInfo> GetAll() =>
        _db.Agents.AsNoTracking().Select(Map).ToList();

    public AgentInfo? GetById(string id)
    {
        var entity = _db.Agents.AsNoTracking().FirstOrDefault(x => x.Id == id);
        return entity is null ? null : Map(entity);
    }

    public AgentInfo Register(AgentInfo agent)
    {
        var existing = _db.Agents.FirstOrDefault(x => x.Id == agent.Id);
        if (existing is not null)
        {
            existing.Hostname = agent.Hostname;
            existing.Os = agent.Os;
            existing.Status = agent.Status;
            existing.LastSeenAt = agent.LastSeenAt;
            existing.PublicUrl = agent.PublicUrl;
            existing.LabelsJson = SerializeDict(agent.Labels);
            existing.CapabilitiesJson = SerializeList(agent.Capabilities);
            existing.Token = agent.Token;
            existing.IsDeleted = false;
            existing.DeletedAt = null;
        }
        else
        {
            _db.Agents.Add(MapToEntity(agent));
        }

        _db.SaveChanges();
        return agent;
    }

    public AgentInfo UpdateHeartbeat(string id, DateTimeOffset lastSeenAt, AgentStatus status)
    {
        var entity = _db.Agents.FirstOrDefault(x => x.Id == id);
        if (entity is null)
        {
            entity = new AgentEntity
            {
                Id = id,
                Hostname = "unknown",
                Os = "unknown",
                Status = status,
                LastSeenAt = lastSeenAt,
                PublicUrl = string.Empty,
                LabelsJson = "{}",
                CapabilitiesJson = "[]",
                Token = Guid.NewGuid().ToString("N")
            };
            _db.Agents.Add(entity);
        }
        else
        {
            entity.Status = status;
            entity.LastSeenAt = lastSeenAt;
        }

        _db.SaveChanges();
        return Map(entity);
    }

    private static AgentInfo Map(AgentEntity e) =>
        new(e.Id, e.Hostname, e.Os, e.Status, e.LastSeenAt, e.PublicUrl,
            DeserializeDict(e.LabelsJson), DeserializeList(e.CapabilitiesJson), e.Token);

    private static AgentEntity MapToEntity(AgentInfo a) =>
        new()
        {
            Id = a.Id,
            Hostname = a.Hostname,
            Os = a.Os,
            Status = a.Status,
            LastSeenAt = a.LastSeenAt,
            PublicUrl = a.PublicUrl,
            LabelsJson = SerializeDict(a.Labels),
            CapabilitiesJson = SerializeList(a.Capabilities),
            Token = a.Token
        };

    private static IReadOnlyDictionary<string, string> DeserializeDict(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
              ?? new Dictionary<string, string>();

    private static IReadOnlyList<string> DeserializeList(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(json)
              ?? new List<string>();

    private static string SerializeDict(IReadOnlyDictionary<string, string>? dict) =>
        dict is null ? "{}" : System.Text.Json.JsonSerializer.Serialize(dict);

    private static string SerializeList(IReadOnlyList<string>? list) =>
        list is null ? "[]" : System.Text.Json.JsonSerializer.Serialize(list);
}

public sealed class EfAgentTicketStore : IAgentTicketStore
{
    private readonly PamGatewayDbContext _db;

    public EfAgentTicketStore(PamGatewayDbContext db) => _db = db;

    public IReadOnlyList<AgentSessionTicket> GetAll() =>
        _db.AgentTickets.AsNoTracking()
            .Select(e => new AgentSessionTicket(e.Ticket, e.SessionId, e.AgentId, e.ExpiresAt))
            .ToList();

    public AgentSessionTicket Issue(string sessionId, string agentId, DateTimeOffset expiresAt)
    {
        var ticket = Guid.NewGuid().ToString("N");
        var entity = new AgentTicketEntity
        {
            Ticket = ticket,
            SessionId = sessionId,
            AgentId = agentId,
            ExpiresAt = expiresAt
        };
        _db.AgentTickets.Add(entity);
        _db.SaveChanges();
        return new AgentSessionTicket(ticket, sessionId, agentId, expiresAt);
    }

    public AgentSessionTicket? GetByTicket(string ticket)
    {
        var entity = _db.AgentTickets.AsNoTracking().FirstOrDefault(x => x.Ticket == ticket);
        return entity is null ? null : new AgentSessionTicket(entity.Ticket, entity.SessionId, entity.AgentId, entity.ExpiresAt);
    }

    public void Revoke(string ticket)
    {
        var entity = _db.AgentTickets.FirstOrDefault(x => x.Ticket == ticket);
        if (entity is not null)
        {
            _db.AgentTickets.Remove(entity);
            _db.SaveChanges();
        }
    }
}
