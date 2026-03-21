using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PamGateway.Api;
using PamGateway.Core;
using PamGateway.Integrations;
using PamGateway.Worker;

namespace PamGateway.Tests.Unit;

public sealed class SlaEscalationTests
{
    private readonly InMemoryAccessRequestStore _requestStore = new();
    private readonly InMemoryTargetStore _targetStore = new(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    private readonly InMemoryAuditStore _auditStore = new();
    private readonly InMemorySessionStore _sessionStore = new();
    private readonly InMemoryAgentTicketStore _ticketStore = new();
    private readonly IItsmClient _itsmClient = Substitute.For<IItsmClient>();

    private AccessRequestWorker CreateWorker(int escalationTimeoutMinutes = 60, bool slaEnabled = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccessRequestStore>(_requestStore);
        services.AddSingleton<ITargetStore>(_targetStore);
        services.AddSingleton<IAuditStore>(_auditStore);
        services.AddSingleton<ISessionStore>(_sessionStore);
        services.AddSingleton<IAgentTicketStore>(_ticketStore);
        services.AddSingleton(_itsmClient);
        services.Configure<SlaOptions>(o =>
        {
            o.EscalationTimeoutMinutes = escalationTimeoutMinutes;
            o.Enabled = slaEnabled;
        });
        var provider = services.BuildServiceProvider();
        return new AccessRequestWorker(
            Substitute.For<ILogger<AccessRequestWorker>>(), provider);
    }

    [Fact]
    public async Task EscalatePendingRequests_EscalatesOldPendingRequests()
    {
        var old = new AccessRequest("REQ-OLD", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Pending,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(1), null);
        _requestStore.Add(old);

        var worker = CreateWorker(escalationTimeoutMinutes: 60);
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(600);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _auditStore.GetAll().Should().Contain(e =>
            e.EventType == "access.sla_escalation" && e.RequestId == "REQ-OLD");
    }

    [Fact]
    public async Task EscalatePendingRequests_DoesNotEscalateRecentRequests()
    {
        var recent = new AccessRequest("REQ-NEW", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddHours(1), null);
        _requestStore.Add(recent);

        var worker = CreateWorker(escalationTimeoutMinutes: 60);
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(600);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _auditStore.GetAll().Should().NotContain(e =>
            e.EventType == "access.sla_escalation");
    }

    [Fact]
    public async Task EscalatePendingRequests_SkipsApprovedRequests()
    {
        var approved = new AccessRequest("REQ-APR", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Approved,
            DateTimeOffset.UtcNow.AddHours(-3),
            DateTimeOffset.UtcNow.AddHours(1), null);
        _requestStore.Add(approved);

        var worker = CreateWorker(escalationTimeoutMinutes: 60);
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(600);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _auditStore.GetAll().Should().NotContain(e =>
            e.EventType == "access.sla_escalation");
    }

    [Fact]
    public async Task EscalatePendingRequests_DisabledSla_NoEscalation()
    {
        var old = new AccessRequest("REQ-X", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Pending,
            DateTimeOffset.UtcNow.AddHours(-5),
            DateTimeOffset.UtcNow.AddHours(1), null);
        _requestStore.Add(old);

        var worker = CreateWorker(slaEnabled: false);
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(600);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _auditStore.GetAll().Should().NotContain(e =>
            e.EventType == "access.sla_escalation");
    }

    [Fact]
    public async Task EscalatePendingRequests_UpdatesItsmWithRetry()
    {
        var old = new AccessRequest("REQ-ITSM", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Pending,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(1), "JIRA-99");
        _requestStore.Add(old);

        var worker = CreateWorker(escalationTimeoutMinutes: 60);
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(600);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        await _itsmClient.Received(1).UpdateStatusAsync("JIRA-99", "escalated", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EscalatePendingRequests_RetriesOnItsmFailure()
    {
        var callCount = 0;
        _itsmClient.UpdateStatusAsync("JIRA-FAIL", "escalated", Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount <= 2)
                    throw new HttpRequestException("fail");
                return Task.CompletedTask;
            });

        var old = new AccessRequest("REQ-RETRY", "T1", "user1", 60, "Reason",
            AccessRequestStatus.Pending,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(1), "JIRA-FAIL");
        _requestStore.Add(old);

        var worker = CreateWorker(escalationTimeoutMinutes: 60);
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(10_000);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        callCount.Should().Be(3);
    }
}
