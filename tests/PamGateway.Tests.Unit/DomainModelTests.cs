using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class DomainModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Theory]
    [InlineData(AccessRequestStatus.Pending, 0)]
    [InlineData(AccessRequestStatus.Approved, 1)]
    [InlineData(AccessRequestStatus.Denied, 2)]
    [InlineData(AccessRequestStatus.Expired, 3)]
    public void AccessRequestStatus_HasExpectedValues(AccessRequestStatus status, int expected)
        => ((int)status).Should().Be(expected);

    [Theory]
    [InlineData(SessionStatus.Pending, 0)]
    [InlineData(SessionStatus.Active, 1)]
    [InlineData(SessionStatus.Terminated, 2)]
    public void SessionStatus_HasExpectedValues(SessionStatus status, int expected)
        => ((int)status).Should().Be(expected);

    [Theory]
    [InlineData(RecordingStatus.Recording, 0)]
    [InlineData(RecordingStatus.Completed, 1)]
    [InlineData(RecordingStatus.Failed, 2)]
    public void RecordingStatus_HasExpectedValues(RecordingStatus status, int expected)
        => ((int)status).Should().Be(expected);

    [Theory]
    [InlineData(AgentStatus.Pending, 0)]
    [InlineData(AgentStatus.Online, 1)]
    [InlineData(AgentStatus.Offline, 2)]
    public void AgentStatus_HasExpectedValues(AgentStatus status, int expected)
        => ((int)status).Should().Be(expected);

    [Fact]
    public void AccessRequestStatus_HasExactly4Members()
        => Enum.GetValues<AccessRequestStatus>().Should().HaveCount(4);

    [Fact]
    public void SessionStatus_HasExactly3Members()
        => Enum.GetValues<SessionStatus>().Should().HaveCount(3);

    [Fact]
    public void RecordingStatus_HasExactly3Members()
        => Enum.GetValues<RecordingStatus>().Should().HaveCount(3);

    [Fact]
    public void AgentStatus_HasExactly3Members()
        => Enum.GetValues<AgentStatus>().Should().HaveCount(3);

    [Fact]
    public void TargetSystem_RoundTripsJson()
    {
        var target = new TargetSystem("T1", "Server", "10.0.0.1", 22,
            new Dictionary<string, string> { ["os"] = "linux" },
            "SSH", "prod", "critical", "Active");

        var json = JsonSerializer.Serialize(target, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TargetSystem>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(target);
    }

    [Fact]
    public void TargetSystem_WithNullOptionalFields_RoundTrips()
    {
        var target = new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active");

        var json = JsonSerializer.Serialize(target, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<TargetSystem>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(target);
        deserialized!.Host.Should().BeNull();
        deserialized.Port.Should().BeNull();
        deserialized.Labels.Should().BeNull();
    }

    [Fact]
    public void AccessRequest_RoundTripsJson()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new AccessRequest("REQ-1", "T1", "user1", 60, "Need access",
            AccessRequestStatus.Pending, now, now.AddMinutes(60), "JIRA-123");

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AccessRequest>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(request);
    }

    [Fact]
    public void AccessRequest_WithNullItsmKey_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new AccessRequest("REQ-1", "T1", "user1", 60, "Need access",
            AccessRequestStatus.Approved, now, now.AddMinutes(60), null);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AccessRequest>(json, JsonOptions);

        deserialized!.ItsmKey.Should().BeNull();
        deserialized.Status.Should().Be(AccessRequestStatus.Approved);
    }

    [Fact]
    public void Session_RoundTripsJson()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new Session("S1", "T1", "REQ-1", "SSH", SessionStatus.Active, now, null);

        var json = JsonSerializer.Serialize(session, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Session>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(session);
    }

    [Fact]
    public void Session_WithEndedAt_RoundTrips()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new Session("S1", "T1", "REQ-1", "SSH", SessionStatus.Terminated, now, now.AddHours(1));

        var json = JsonSerializer.Serialize(session, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Session>(json, JsonOptions);

        deserialized!.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public void SessionRecording_RoundTripsJson()
    {
        var now = DateTimeOffset.UtcNow;
        var recording = new SessionRecording("R1", "S1", "node", "/data/r1",
            RecordingStatus.Completed, now, now.AddMinutes(30), 1024, "abc123");

        var json = JsonSerializer.Serialize(recording, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<SessionRecording>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(recording);
    }

    [Fact]
    public void AuditEvent_RoundTripsJson()
    {
        var audit = new AuditEvent(DateTimeOffset.UtcNow, "access.requested", "U1", "admin",
            "PAM_Administrator", "T1", "Server", "request", "success", "REQ-1", "", "10.0.0.1");

        var json = JsonSerializer.Serialize(audit, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AuditEvent>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(audit);
    }

    [Fact]
    public void Role_RoundTripsJson()
    {
        var role = new Role("R1", "Admin", "Administrator role");
        var json = JsonSerializer.Serialize(role, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Role>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(role);
    }

    [Fact]
    public void Policy_RoundTripsJson()
    {
        var policy = new Policy("P1", "Allow SSH", "SSH", "SSH,SFTP", "Allow",
            new Dictionary<string, string> { ["os"] = "linux" });

        var json = JsonSerializer.Serialize(policy, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Policy>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(policy);
    }

    [Fact]
    public void Policy_WithNullLabels_RoundTrips()
    {
        var policy = new Policy("P1", "Allow All", "*", "*", "Allow", null);

        var json = JsonSerializer.Serialize(policy, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Policy>(json, JsonOptions);

        deserialized!.TargetLabelSelector.Should().BeNull();
    }

    [Fact]
    public void Approval_RoundTripsJson()
    {
        var approval = new Approval("APR-1", "REQ-1", "admin", DateTimeOffset.UtcNow, "approved");
        var json = JsonSerializer.Serialize(approval, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<Approval>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(approval);
    }

    [Fact]
    public void AgentInfo_RoundTripsJson()
    {
        var agent = new AgentInfo("A1", "host1", "linux", AgentStatus.Online,
            DateTimeOffset.UtcNow, "http://agent:7071",
            new Dictionary<string, string> { ["zone"] = "dmz" },
            new List<string> { "ssh", "rdp" }, "token123");

        var json = JsonSerializer.Serialize(agent, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentInfo>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(agent);
    }

    [Fact]
    public void AgentSessionTicket_RoundTripsJson()
    {
        var ticket = new AgentSessionTicket("TKT-1", "S1", "A1", DateTimeOffset.UtcNow.AddMinutes(5));
        var json = JsonSerializer.Serialize(ticket, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<AgentSessionTicket>(json, JsonOptions);

        deserialized.Should().BeEquivalentTo(ticket);
    }

    [Fact]
    public void AccessRequest_WithExpression_SupportsRecordWith()
    {
        var now = DateTimeOffset.UtcNow;
        var original = new AccessRequest("REQ-1", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Pending, now, now.AddMinutes(60), null);

        var approved = original with { Status = AccessRequestStatus.Approved };

        approved.Id.Should().Be(original.Id);
        approved.Status.Should().Be(AccessRequestStatus.Approved);
        original.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public void Session_WithExpression_SupportsRecordWith()
    {
        var now = DateTimeOffset.UtcNow;
        var original = new Session("S1", "T1", "REQ-1", "SSH", SessionStatus.Active, now, null);
        var terminated = original with { Status = SessionStatus.Terminated, EndedAt = now.AddHours(1) };

        terminated.Status.Should().Be(SessionStatus.Terminated);
        terminated.EndedAt.Should().NotBeNull();
        original.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public void Enum_JsonSerialization_UsesStringNames()
    {
        var request = new AccessRequest("REQ-1", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Approved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        json.Should().Contain("\"Approved\"").And.NotContain("\"1\"");
    }
}
