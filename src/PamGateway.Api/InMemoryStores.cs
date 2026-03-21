using PamGateway.Core;

namespace PamGateway.Api;

public sealed class InMemoryAccessRequestStore : IAccessRequestStore
{
    private readonly List<AccessRequest> _items = new();

    public IReadOnlyList<AccessRequest> GetAll() => _items;

    public AccessRequest? GetById(string id) => _items.Find(item => item.Id == id);

    public AccessRequest? GetByItsmKey(string itsmKey)
        => _items.Find(item => string.Equals(item.ItsmKey, itsmKey, StringComparison.OrdinalIgnoreCase));

    public AccessRequest Add(AccessRequest request)
    {
        _items.Add(request);
        return request;
    }

    public AccessRequest Update(AccessRequest request)
    {
        var index = _items.FindIndex(item => item.Id == request.Id);
        if (index >= 0)
        {
            _items[index] = request;
        }

        return request;
    }
}

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly List<Session> _items = new();

    public IReadOnlyList<Session> GetAll() => _items;

    public Session? GetById(string id) => _items.Find(item => item.Id == id);

    public Session Add(Session session)
    {
        _items.Add(session);
        return session;
    }

    public Session Update(Session session)
    {
        var index = _items.FindIndex(item => item.Id == session.Id);
        if (index >= 0)
        {
            _items[index] = session;
        }

        return session;
    }
}

public sealed class InMemoryRecordingStore : IRecordingStore
{
    private readonly List<SessionRecording> _items = new();

    public IReadOnlyList<SessionRecording> GetAll() => _items;

    public SessionRecording? GetById(string id) => _items.Find(item => item.Id == id);

    public SessionRecording Add(SessionRecording recording)
    {
        _items.Add(recording);
        return recording;
    }

    public SessionRecording Update(SessionRecording recording)
    {
        var index = _items.FindIndex(item => item.Id == recording.Id);
        if (index >= 0)
        {
            _items[index] = recording;
        }
        else
        {
            _items.Add(recording);
        }

        return recording;
    }
}

public sealed class InMemoryTargetStore : ITargetStore
{
    private readonly List<TargetSystem> _items;

    public InMemoryTargetStore(IConfiguration configuration)
    {
        _items = configuration.GetSection("Targets").Get<List<TargetSystem>>() ?? new List<TargetSystem>();
    }

    public IReadOnlyList<TargetSystem> GetAll() => _items;

    public TargetSystem? GetById(string id) => _items.Find(item => item.Id == id);

    public void AddOrUpdate(TargetSystem target)
    {
        var index = _items.FindIndex(item => item.Id == target.Id);
        if (index >= 0)
        {
            _items[index] = target;
        }
        else
        {
            _items.Add(target);
        }
    }

    public void AddOrUpdateRange(IEnumerable<TargetSystem> targets)
    {
        foreach (var target in targets)
        {
            AddOrUpdate(target);
        }
    }
}

public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditEvent> _items = new();

    public IReadOnlyList<AuditEvent> GetAll() => _items;

    public void Add(AuditEvent auditEvent) => _items.Add(auditEvent);
}

public sealed class InMemoryRoleStore : IRoleStore
{
    private readonly List<Role> _items = new();

    public IReadOnlyList<Role> GetAll() => _items;

    public Role? GetById(string id) => _items.Find(item => item.Id == id);

    public Role Add(Role role)
    {
        _items.Add(role);
        return role;
    }
}

public sealed class InMemoryPolicyStore : IPolicyStore
{
    private readonly List<Policy> _items = new();

    public IReadOnlyList<Policy> GetAll() => _items;

    public Policy? GetById(string id) => _items.Find(item => item.Id == id);

    public Policy Add(Policy policy)
    {
        _items.Add(policy);
        return policy;
    }

    public Policy Update(Policy policy)
    {
        var index = _items.FindIndex(item => string.Equals(item.Id, policy.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _items[index] = policy;
            return policy;
        }

        _items.Add(policy);
        return policy;
    }
}

public sealed class InMemoryApprovalStore : IApprovalStore
{
    private readonly List<Approval> _items = new();

    public IReadOnlyList<Approval> GetAll() => _items;

    public Approval Add(Approval approval)
    {
        _items.Add(approval);
        return approval;
    }
}

public sealed class InMemoryAgentStore : IAgentStore
{
    private readonly Dictionary<string, AgentInfo> _items = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AgentInfo> GetAll() => _items.Values.ToList();

    public AgentInfo? GetById(string id) => _items.TryGetValue(id, out var agent) ? agent : null;

    public AgentInfo Register(AgentInfo agent)
    {
        _items[agent.Id] = agent;
        return agent;
    }

    public AgentInfo UpdateHeartbeat(string id, DateTimeOffset lastSeenAt, AgentStatus status)
    {
        if (!_items.TryGetValue(id, out var agent))
        {
            agent = new AgentInfo(
                id,
                "unknown",
                "unknown",
                status,
                lastSeenAt,
                string.Empty,
                new Dictionary<string, string>(),
                Array.Empty<string>(),
                Guid.NewGuid().ToString("N"));
        }

        agent = agent with { Status = status, LastSeenAt = lastSeenAt };
        _items[id] = agent;
        return agent;
    }
}

public sealed class InMemoryAgentTicketStore : IAgentTicketStore
{
    private readonly Dictionary<string, AgentSessionTicket> _items = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AgentSessionTicket> GetAll() => _items.Values.ToList();

    public AgentSessionTicket Issue(string sessionId, string agentId, DateTimeOffset expiresAt)
    {
        var ticket = Guid.NewGuid().ToString("N");
        var model = new AgentSessionTicket(ticket, sessionId, agentId, expiresAt);
        _items[ticket] = model;
        return model;
    }

    public AgentSessionTicket? GetByTicket(string ticket)
        => _items.TryGetValue(ticket, out var model) ? model : null;

    public void Revoke(string ticket) => _items.Remove(ticket);
}

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly List<Credential> _items = new();

    public IReadOnlyList<Credential> GetAll() => _items;
    public Credential? GetById(string id) => _items.Find(c => c.Id == id);
    public IReadOnlyList<Credential> GetByTargetId(string targetId)
        => _items.Where(c => c.TargetId == targetId).ToList();

    public Credential Add(Credential credential)
    {
        _items.Add(credential);
        return credential;
    }

    public Credential Update(Credential credential)
    {
        var idx = _items.FindIndex(c => c.Id == credential.Id);
        if (idx >= 0) _items[idx] = credential;
        return credential;
    }
}

public sealed class InMemoryCredentialCheckoutStore : ICredentialCheckoutStore
{
    private readonly List<CredentialCheckout> _items = new();

    public IReadOnlyList<CredentialCheckout> GetAll() => _items;
    public CredentialCheckout? GetById(string id) => _items.Find(c => c.Id == id);
    public IReadOnlyList<CredentialCheckout> GetByCredentialId(string credentialId)
        => _items.Where(c => c.CredentialId == credentialId).ToList();

    public CredentialCheckout Add(CredentialCheckout checkout)
    {
        _items.Add(checkout);
        return checkout;
    }

    public CredentialCheckout Update(CredentialCheckout checkout)
    {
        var idx = _items.FindIndex(c => c.Id == checkout.Id);
        if (idx >= 0) _items[idx] = checkout;
        return checkout;
    }
}
