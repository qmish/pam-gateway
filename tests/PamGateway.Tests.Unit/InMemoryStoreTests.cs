using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class InMemoryAccessRequestStoreTests
{
    private readonly InMemoryAccessRequestStore _store = new();

    private static AccessRequest CreateRequest(string id = "REQ-1", AccessRequestStatus status = AccessRequestStatus.Pending)
        => new(id, "T1", "user1", 60, "reason", status, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null);

    [Fact]
    public void Add_And_GetById()
    {
        var req = CreateRequest();
        _store.Add(req);
        _store.GetById("REQ-1").Should().Be(req);
    }

    [Fact]
    public void GetById_NotFound_ReturnsNull()
    {
        _store.GetById("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllItems()
    {
        _store.Add(CreateRequest("REQ-1"));
        _store.Add(CreateRequest("REQ-2"));
        _store.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void Update_ReplacesExisting()
    {
        _store.Add(CreateRequest("REQ-1", AccessRequestStatus.Pending));
        var updated = CreateRequest("REQ-1", AccessRequestStatus.Approved);
        _store.Update(updated);
        _store.GetById("REQ-1")!.Status.Should().Be(AccessRequestStatus.Approved);
    }

    [Fact]
    public void GetByItsmKey_FindsByKey()
    {
        var req = CreateRequest() with { ItsmKey = "JIRA-100" };
        _store.Add(req);
        _store.GetByItsmKey("JIRA-100").Should().Be(req);
        _store.GetByItsmKey("jira-100").Should().Be(req);
    }

    [Fact]
    public void GetByItsmKey_NotFound_ReturnsNull()
    {
        _store.GetByItsmKey("JIRA-MISSING").Should().BeNull();
    }
}

public sealed class InMemorySessionStoreTests
{
    private readonly InMemorySessionStore _store = new();

    private static Session CreateSession(string id = "SES-1", SessionStatus status = SessionStatus.Active)
        => new(id, "T1", "REQ-1", "ssh", status, DateTimeOffset.UtcNow, null);

    [Fact]
    public void CRUD_Operations()
    {
        var session = CreateSession();
        _store.Add(session);

        _store.GetById("SES-1").Should().Be(session);
        _store.GetAll().Should().HaveCount(1);

        var updated = session with { Status = SessionStatus.Terminated, EndedAt = DateTimeOffset.UtcNow };
        _store.Update(updated);
        _store.GetById("SES-1")!.Status.Should().Be(SessionStatus.Terminated);
    }

    [Fact]
    public void GetById_NotFound_ReturnsNull()
    {
        _store.GetById("nonexistent").Should().BeNull();
    }
}

public sealed class InMemoryTargetStoreTests
{
    private static InMemoryTargetStore CreateStore()
    {
        var config = new ConfigurationBuilder().Build();
        return new InMemoryTargetStore(config);
    }

    private static TargetSystem CreateTarget(string id = "T1")
        => new(id, "Server1", "10.0.0.1", 22, null, "Linux", "prod", "critical", "active");

    [Fact]
    public void AddOrUpdate_AddsNew()
    {
        var store = CreateStore();
        store.AddOrUpdate(CreateTarget());
        store.GetById("T1").Should().NotBeNull();
        store.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public void AddOrUpdate_UpdatesExisting()
    {
        var store = CreateStore();
        store.AddOrUpdate(CreateTarget());
        var updated = CreateTarget() with { Name = "UpdatedServer" };
        store.AddOrUpdate(updated);
        store.GetAll().Should().HaveCount(1);
        store.GetById("T1")!.Name.Should().Be("UpdatedServer");
    }

    [Fact]
    public void AddOrUpdateRange_AddsMultiple()
    {
        var store = CreateStore();
        store.AddOrUpdateRange(new[] { CreateTarget("T1"), CreateTarget("T2") });
        store.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void GetById_NotFound_ReturnsNull()
    {
        CreateStore().GetById("nonexistent").Should().BeNull();
    }
}

public sealed class InMemoryRecordingStoreTests
{
    private readonly InMemoryRecordingStore _store = new();

    private static SessionRecording CreateRecording(string id = "REC-1")
        => new(id, "SES-1", "node", null, RecordingStatus.Recording, DateTimeOffset.UtcNow, null, null, null);

    [Fact]
    public void CRUD_Operations()
    {
        var rec = CreateRecording();
        _store.Add(rec);
        _store.GetById("REC-1").Should().Be(rec);

        var updated = rec with { Status = RecordingStatus.Completed };
        _store.Update(updated);
        _store.GetById("REC-1")!.Status.Should().Be(RecordingStatus.Completed);
    }

    [Fact]
    public void Update_NonExisting_Adds()
    {
        var rec = CreateRecording("REC-NEW");
        _store.Update(rec);
        _store.GetById("REC-NEW").Should().NotBeNull();
    }
}

public sealed class InMemoryAuditStoreTests
{
    private readonly InMemoryAuditStore _store = new();

    [Fact]
    public void Add_And_GetAll()
    {
        var evt = new AuditEvent(DateTimeOffset.UtcNow, "test", "u1", "user1", "admin", "T1", "Server", "action", "success", "", "", "127.0.0.1");
        _store.Add(evt);
        _store.GetAll().Should().ContainSingle().Which.Should().Be(evt);
    }
}

public sealed class InMemoryRoleStoreTests
{
    private readonly InMemoryRoleStore _store = new();

    [Fact]
    public void Add_And_GetById()
    {
        var role = new Role("R1", "Admin", "Administrator");
        _store.Add(role);
        _store.GetById("R1").Should().Be(role);
        _store.GetAll().Should().HaveCount(1);
    }
}

public sealed class InMemoryPolicyStoreTests
{
    private readonly InMemoryPolicyStore _store = new();

    [Fact]
    public void Add_And_GetById()
    {
        var policy = new Policy("P1", "AllowAll", "*", "*", "allow", null);
        _store.Add(policy);
        _store.GetById("P1").Should().Be(policy);
    }

    [Fact]
    public void Update_ReplacesExisting()
    {
        _store.Add(new Policy("P1", "AllowAll", "*", "*", "allow", null));
        var updated = new Policy("P1", "DenyAll", "*", "*", "deny", null);
        _store.Update(updated);
        _store.GetById("P1")!.Effect.Should().Be("deny");
        _store.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public void Update_NonExisting_Adds()
    {
        var policy = new Policy("P-NEW", "New", "*", "*", "allow", null);
        _store.Update(policy);
        _store.GetById("P-NEW").Should().NotBeNull();
    }
}

public sealed class InMemoryApprovalStoreTests
{
    private readonly InMemoryApprovalStore _store = new();

    [Fact]
    public void Add_And_GetAll()
    {
        var approval = new Approval("A1", "REQ-1", "admin", DateTimeOffset.UtcNow, "approved");
        _store.Add(approval);
        _store.GetAll().Should().ContainSingle().Which.Should().Be(approval);
    }
}

public sealed class InMemoryAgentStoreTests
{
    private readonly InMemoryAgentStore _store = new();

    private static AgentInfo CreateAgent(string id = "agent-1")
        => new(id, "host1", "linux", AgentStatus.Online, DateTimeOffset.UtcNow,
            "http://agent:7071", new Dictionary<string, string>(), new[] { "ssh" }, "token123");

    [Fact]
    public void Register_And_GetById()
    {
        var agent = CreateAgent();
        _store.Register(agent);
        _store.GetById("agent-1").Should().Be(agent);
    }

    [Fact]
    public void Register_CaseInsensitive()
    {
        _store.Register(CreateAgent("Agent-1"));
        _store.GetById("agent-1").Should().NotBeNull();
    }

    [Fact]
    public void Register_OverwritesExisting()
    {
        _store.Register(CreateAgent());
        var updated = CreateAgent() with { Hostname = "host2" };
        _store.Register(updated);
        _store.GetAll().Should().HaveCount(1);
        _store.GetById("agent-1")!.Hostname.Should().Be("host2");
    }

    [Fact]
    public void UpdateHeartbeat_UpdatesStatusAndTime()
    {
        _store.Register(CreateAgent());
        var newTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var result = _store.UpdateHeartbeat("agent-1", newTime, AgentStatus.Online);
        result.LastSeenAt.Should().Be(newTime);
        result.Status.Should().Be(AgentStatus.Online);
    }

    [Fact]
    public void UpdateHeartbeat_NonExisting_CreatesPlaceholder()
    {
        var result = _store.UpdateHeartbeat("new-agent", DateTimeOffset.UtcNow, AgentStatus.Online);
        result.Should().NotBeNull();
        result.Id.Should().Be("new-agent");
        result.Hostname.Should().Be("unknown");
    }

    [Fact]
    public void GetById_NotFound_ReturnsNull()
    {
        _store.GetById("nonexistent").Should().BeNull();
    }
}

public sealed class InMemoryAgentTicketStoreTests
{
    private readonly InMemoryAgentTicketStore _store = new();

    [Fact]
    public void Issue_And_GetByTicket()
    {
        var ticket = _store.Issue("SES-1", "agent-1", DateTimeOffset.UtcNow.AddMinutes(5));
        ticket.Should().NotBeNull();
        ticket.SessionId.Should().Be("SES-1");
        ticket.AgentId.Should().Be("agent-1");

        _store.GetByTicket(ticket.Ticket).Should().Be(ticket);
    }

    [Fact]
    public void Revoke_RemovesTicket()
    {
        var ticket = _store.Issue("SES-1", "agent-1", DateTimeOffset.UtcNow.AddMinutes(5));
        _store.Revoke(ticket.Ticket);
        _store.GetByTicket(ticket.Ticket).Should().BeNull();
    }

    [Fact]
    public void GetByTicket_NotFound_ReturnsNull()
    {
        _store.GetByTicket("nonexistent").Should().BeNull();
    }
}
