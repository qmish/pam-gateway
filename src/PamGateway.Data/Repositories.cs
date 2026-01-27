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
        new(entity.Id, entity.Name, entity.Type, entity.Environment, entity.Criticality, entity.Status);

    private static TargetEntity Map(TargetSystem target) =>
        new()
        {
            Id = target.Id,
            Name = target.Name,
            Type = target.Type,
            Environment = target.Environment,
            Criticality = target.Criticality,
            Status = target.Status
        };
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

    private static Policy Map(PolicyEntity entity) =>
        new(entity.Id, entity.Name, entity.TargetType, entity.AllowedProtocols, entity.Effect);

    private static PolicyEntity Map(Policy policy) =>
        new()
        {
            Id = policy.Id,
            Name = policy.Name,
            TargetType = policy.TargetType,
            AllowedProtocols = policy.AllowedProtocols,
            Effect = policy.Effect
        };
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
