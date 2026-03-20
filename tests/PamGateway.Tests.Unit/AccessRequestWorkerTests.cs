using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PamGateway.Api;
using PamGateway.Core;
using PamGateway.Integrations;
using PamGateway.Worker;

namespace PamGateway.Tests.Unit;

public sealed class AccessRequestWorkerTests
{
    private readonly InMemoryAccessRequestStore _requestStore = new();
    private readonly InMemoryTargetStore _targetStore = new(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    private readonly InMemoryAuditStore _auditStore = new();
    private readonly InMemorySessionStore _sessionStore = new();
    private readonly InMemoryAgentTicketStore _ticketStore = new();
    private readonly IItsmClient _itsmClient = Substitute.For<IItsmClient>();

    private AccessRequestWorker CreateWorker()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccessRequestStore>(_requestStore);
        services.AddSingleton<ITargetStore>(_targetStore);
        services.AddSingleton<IAuditStore>(_auditStore);
        services.AddSingleton<ISessionStore>(_sessionStore);
        services.AddSingleton<IAgentTicketStore>(_ticketStore);
        services.AddSingleton(_itsmClient);
        var provider = services.BuildServiceProvider();

        var logger = Substitute.For<ILogger<AccessRequestWorker>>();
        return new AccessRequestWorker(logger, provider);
    }

    private static AccessRequest CreateRequest(
        string id = "REQ-1",
        AccessRequestStatus status = AccessRequestStatus.Pending,
        int minutesUntilExpiry = -5,
        string? itsmKey = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new AccessRequest(id, "T1", "user1", 60, "Reason",
            status, now.AddHours(-1), now.AddMinutes(minutesUntilExpiry), itsmKey);
    }

    [Fact]
    public async Task ProcessExpiredRequests_MarksExpiredRequestsAsExpired()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-1", minutesUntilExpiry: -5));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();

        // StartAsync triggers ExecuteAsync, which processes and then delays 1 min
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        var request = _requestStore.GetById("REQ-1");
        request!.Status.Should().Be(AccessRequestStatus.Expired);
    }

    [Fact]
    public async Task ProcessExpiredRequests_WritesAuditEvent()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-1", minutesUntilExpiry: -5));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _auditStore.GetAll().Should().ContainSingle(e =>
            e.EventType == "access.expired" && e.RequestId == "REQ-1");
    }

    [Fact]
    public async Task ProcessExpiredRequests_UpdatesItsmStatus()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-1", minutesUntilExpiry: -5, itsmKey: "JIRA-100"));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        await _itsmClient.Received(1).UpdateStatusAsync("JIRA-100", "expired", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessExpiredRequests_SkipsNonExpiredRequests()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-FUTURE", minutesUntilExpiry: 60));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        var request = _requestStore.GetById("REQ-FUTURE");
        request!.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public async Task ProcessExpiredRequests_SkipsAlreadyExpiredRequests()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-DONE", status: AccessRequestStatus.Expired, minutesUntilExpiry: -5));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _auditStore.GetAll().Should().NotContain(e => e.RequestId == "REQ-DONE");
    }

    [Fact]
    public async Task ProcessExpiredRequests_HandlesItsmError_Gracefully()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-ERR", minutesUntilExpiry: -5, itsmKey: "JIRA-ERR"));
        _itsmClient.UpdateStatusAsync("JIRA-ERR", "expired", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Jira unavailable"));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        var request = _requestStore.GetById("REQ-ERR");
        request!.Status.Should().Be(AccessRequestStatus.Expired);
    }

    [Fact]
    public async Task ProcessExpiredRequests_WithNoItsmKey_DoesNotCallItsm()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null,
            "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-NOITSM", minutesUntilExpiry: -5, itsmKey: null));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        await _itsmClient.DidNotReceive().UpdateStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeSessions_TerminatesActiveSessionForExpiredRequest()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Server", null, null, null, "SSH", "prod", "critical", "Active"));
        _requestStore.Add(CreateRequest("REQ-EXP", status: AccessRequestStatus.Expired, minutesUntilExpiry: -5));
        _sessionStore.Add(new Session("SES-1", "T1", "REQ-EXP", "ssh", SessionStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-30), null));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        var session = _sessionStore.GetById("SES-1");
        session!.Status.Should().Be(SessionStatus.Terminated);
        session.EndedAt.Should().NotBeNull();
        _auditStore.GetAll().Should().Contain(e => e.EventType == "session.revoked" && e.SessionId == "SES-1");
    }

    [Fact]
    public async Task RevokeSessions_SkipsAlreadyTerminatedSession()
    {
        _requestStore.Add(CreateRequest("REQ-EXP2", status: AccessRequestStatus.Expired, minutesUntilExpiry: -5));
        _sessionStore.Add(new Session("SES-TERM", "T1", "REQ-EXP2", "ssh", SessionStatus.Terminated,
            DateTimeOffset.UtcNow.AddMinutes(-30), DateTimeOffset.UtcNow.AddMinutes(-10)));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _auditStore.GetAll().Should().NotContain(e => e.SessionId == "SES-TERM" && e.EventType == "session.revoked");
    }

    [Fact]
    public async Task CleanupExpiredTickets_RevokesExpiredTickets()
    {
        _ticketStore.Issue("SES-A", "AGENT-A", DateTimeOffset.UtcNow.AddMinutes(-10));
        _ticketStore.Issue("SES-B", "AGENT-B", DateTimeOffset.UtcNow.AddHours(1));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _ticketStore.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public async Task ConsistencyCheck_TerminatesSessionForDeniedRequest()
    {
        _requestStore.Add(CreateRequest("REQ-DENIED", status: AccessRequestStatus.Denied, minutesUntilExpiry: 60));
        _sessionStore.Add(new Session("SES-BAD", "T1", "REQ-DENIED", "ssh", SessionStatus.Active,
            DateTimeOffset.UtcNow.AddMinutes(-10), null));

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        var session = _sessionStore.GetById("SES-BAD");
        session!.Status.Should().Be(SessionStatus.Terminated);
        _auditStore.GetAll().Should().Contain(e => e.EventType == "session.consistency_fix");
    }
}
