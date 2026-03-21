using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PamGateway.Core;
using PamGateway.Data;

namespace PamGateway.Tests.Unit;

public sealed class EfAgentStoreTests : IDisposable
{
    private readonly PamGatewayDbContext _db;
    private readonly EfAgentStore _store;

    public EfAgentStoreTests()
    {
        var options = new DbContextOptionsBuilder<PamGatewayDbContext>()
            .UseInMemoryDatabase($"EfAgentStore_{Guid.NewGuid()}")
            .Options;
        _db = new PamGatewayDbContext(options);
        _store = new EfAgentStore(_db);
    }

    public void Dispose() => _db.Dispose();

    private static AgentInfo MakeAgent(string id = "agent-1") => new(
        id, "host-1", "linux", AgentStatus.Online,
        DateTimeOffset.UtcNow, "http://agent:7071",
        new Dictionary<string, string> { ["zone"] = "dmz" },
        new List<string> { "ssh", "rdp" },
        "token-abc");

    [Fact]
    public void Register_AddsNewAgent()
    {
        var agent = MakeAgent();
        _store.Register(agent);

        var result = _store.GetById("agent-1");
        result.Should().NotBeNull();
        result!.Hostname.Should().Be("host-1");
        result.Labels.Should().ContainKey("zone");
        result.Capabilities.Should().Contain("ssh");
    }

    [Fact]
    public void Register_UpdatesExistingAgent()
    {
        _store.Register(MakeAgent());
        var updated = MakeAgent() with { Hostname = "new-host", Os = "windows" };
        _store.Register(updated);

        var result = _store.GetById("agent-1");
        result!.Hostname.Should().Be("new-host");
        result.Os.Should().Be("windows");
        _store.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public void GetAll_ReturnsAllAgents()
    {
        _store.Register(MakeAgent("a1"));
        _store.Register(MakeAgent("a2"));

        _store.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        _store.GetById("nonexistent").Should().BeNull();
    }

    [Fact]
    public void UpdateHeartbeat_UpdatesExistingAgent()
    {
        _store.Register(MakeAgent());
        var newTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var result = _store.UpdateHeartbeat("agent-1", newTime, AgentStatus.Offline);

        result.Status.Should().Be(AgentStatus.Offline);
        result.LastSeenAt.Should().Be(newTime);
    }

    [Fact]
    public void UpdateHeartbeat_CreatesAgent_WhenNotFound()
    {
        var result = _store.UpdateHeartbeat("new-agent", DateTimeOffset.UtcNow, AgentStatus.Online);

        result.Should().NotBeNull();
        result.Id.Should().Be("new-agent");
        result.Hostname.Should().Be("unknown");
    }
}

public sealed class EfAgentTicketStoreTests : IDisposable
{
    private readonly PamGatewayDbContext _db;
    private readonly EfAgentTicketStore _store;

    public EfAgentTicketStoreTests()
    {
        var options = new DbContextOptionsBuilder<PamGatewayDbContext>()
            .UseInMemoryDatabase($"EfAgentTicket_{Guid.NewGuid()}")
            .Options;
        _db = new PamGatewayDbContext(options);
        _store = new EfAgentTicketStore(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Issue_CreatesTicket()
    {
        var ticket = _store.Issue("sess-1", "agent-1", DateTimeOffset.UtcNow.AddHours(1));

        ticket.Should().NotBeNull();
        ticket.SessionId.Should().Be("sess-1");
        ticket.AgentId.Should().Be("agent-1");
        ticket.Ticket.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetByTicket_ReturnsIssuedTicket()
    {
        var ticket = _store.Issue("sess-1", "agent-1", DateTimeOffset.UtcNow.AddHours(1));
        var result = _store.GetByTicket(ticket.Ticket);

        result.Should().NotBeNull();
        result!.SessionId.Should().Be("sess-1");
    }

    [Fact]
    public void GetByTicket_ReturnsNull_WhenNotFound()
    {
        _store.GetByTicket("no-such-ticket").Should().BeNull();
    }

    [Fact]
    public void Revoke_RemovesTicket()
    {
        var ticket = _store.Issue("sess-1", "agent-1", DateTimeOffset.UtcNow.AddHours(1));
        _store.Revoke(ticket.Ticket);

        _store.GetByTicket(ticket.Ticket).Should().BeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllTickets()
    {
        _store.Issue("s1", "a1", DateTimeOffset.UtcNow.AddHours(1));
        _store.Issue("s2", "a2", DateTimeOffset.UtcNow.AddHours(1));

        _store.GetAll().Should().HaveCount(2);
    }
}
