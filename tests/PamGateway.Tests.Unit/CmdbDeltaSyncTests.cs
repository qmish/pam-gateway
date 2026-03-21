using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Api.Services;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Tests.Unit;

public sealed class CmdbDeltaSyncTests
{
    private readonly ICmdbClient _cmdbClient = Substitute.For<ICmdbClient>();
    private readonly InMemoryTargetStore _targetStore;
    private readonly InMemoryAuditStore _auditStore = new();

    public CmdbDeltaSyncTests()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        _targetStore = new InMemoryTargetStore(config);
    }

    private CmdbSyncService CreateService(CmdbSyncOptions? opts = null)
    {
        opts ??= new CmdbSyncOptions { Enabled = true, IntervalMinutes = 1, DeltaSyncEnabled = true, FullSyncEveryNth = 3 };

        var services = new ServiceCollection();
        services.AddSingleton<ICmdbClient>(_cmdbClient);
        services.AddSingleton<ITargetStore>(_targetStore);
        services.AddSingleton<IAuditStore>(_auditStore);
        var sp = services.BuildServiceProvider();

        return new CmdbSyncService(
            sp,
            Options.Create(opts),
            Substitute.For<ILogger<CmdbSyncService>>());
    }

    [Fact]
    public async Task FirstSync_AlwaysPerformsFullSync()
    {
        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>
            {
                new("T1", "Server A", "SSH", "prod", "critical", "active")
            });

        var svc = CreateService();
        await svc.SyncAsync(CancellationToken.None);

        await _cmdbClient.Received(1).FetchTargetsAsync(Arg.Any<CancellationToken>());
        await _cmdbClient.DidNotReceive().FetchTargetsModifiedSinceAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        _targetStore.GetAll().Should().HaveCount(1);

        var auditEvt = _auditStore.GetAll().First(e => e.EventType == "cmdb.sync");
        auditEvt.SourceIp.Should().Contain("mode=full");
    }

    [Fact]
    public async Task SecondSync_UsesDeltaSync_WhenEnabled()
    {
        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>());
        _cmdbClient.FetchTargetsModifiedSinceAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>
            {
                new("T2", "New Server", "RDP", "staging", "non-critical", "active")
            });

        var svc = CreateService();

        await svc.SyncAsync(CancellationToken.None);
        await svc.SyncAsync(CancellationToken.None);

        await _cmdbClient.Received(1).FetchTargetsAsync(Arg.Any<CancellationToken>());
        await _cmdbClient.Received(1).FetchTargetsModifiedSinceAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        var deltaAudit = _auditStore.GetAll().Last(e => e.EventType == "cmdb.sync");
        deltaAudit.SourceIp.Should().Contain("mode=delta");
    }

    [Fact]
    public async Task DeltaSync_DoesNotCheckConflicts()
    {
        _targetStore.AddOrUpdate(new TargetSystem("LOCAL-1", "Local Only", null, null, null, "SSH", "prod", "critical", "active"));

        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>
            {
                new("LOCAL-1", "Local Only", "SSH", "prod", "critical", "active")
            });
        _cmdbClient.FetchTargetsModifiedSinceAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>());

        var svc = CreateService();

        await svc.SyncAsync(CancellationToken.None);
        await svc.SyncAsync(CancellationToken.None);

        var deltaAudit = _auditStore.GetAll().Last(e => e.EventType == "cmdb.sync");
        deltaAudit.SourceIp.Should().Contain("conflicts=0");
    }

    [Fact]
    public async Task FullSyncEveryNth_ForcesFullSync()
    {
        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>());
        _cmdbClient.FetchTargetsModifiedSinceAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>());

        var svc = CreateService(new CmdbSyncOptions
        {
            Enabled = true, IntervalMinutes = 1, DeltaSyncEnabled = true, FullSyncEveryNth = 3
        });

        for (int i = 0; i < 4; i++)
            await svc.SyncAsync(CancellationToken.None);

        // Calls: 1=full, 2=delta, 3=full, 4=delta
        await _cmdbClient.Received(2).FetchTargetsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeltaSyncDisabled_AlwaysUsesFullSync()
    {
        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>());

        var svc = CreateService(new CmdbSyncOptions
        {
            Enabled = true, IntervalMinutes = 1, DeltaSyncEnabled = false
        });

        await svc.SyncAsync(CancellationToken.None);
        await svc.SyncAsync(CancellationToken.None);

        await _cmdbClient.Received(2).FetchTargetsAsync(Arg.Any<CancellationToken>());
        await _cmdbClient.DidNotReceive().FetchTargetsModifiedSinceAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
