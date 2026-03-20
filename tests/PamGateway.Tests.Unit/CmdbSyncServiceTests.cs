using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PamGateway.Api;
using PamGateway.Api.Services;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Tests.Unit;

public sealed class CmdbSyncServiceTests
{
    private readonly ICmdbClient _cmdbClient = Substitute.For<ICmdbClient>();
    private readonly InMemoryTargetStore _targetStore;
    private readonly InMemoryAuditStore _auditStore = new();

    public CmdbSyncServiceTests()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        _targetStore = new InMemoryTargetStore(config);
    }

    private CmdbSyncService CreateService(CmdbSyncOptions? opts = null)
    {
        opts ??= new CmdbSyncOptions { Enabled = true, IntervalMinutes = 1 };

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
    public async Task SyncAsync_ImportsNewTargets()
    {
        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>
            {
                new("T1", "Server A", "SSH", "prod", "critical", "active"),
                new("T2", "Server B", "RDP", "dev", "non-critical", "active")
            });

        var svc = CreateService();
        await svc.SyncAsync(CancellationToken.None);

        _targetStore.GetAll().Should().HaveCount(2);
        _targetStore.GetById("T1")!.Name.Should().Be("Server A");
        _targetStore.GetById("T2")!.Type.Should().Be("RDP");
    }

    [Fact]
    public async Task SyncAsync_UpdatesExistingTargetMetadata()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Old Name", "10.0.0.1", 22,
            new Dictionary<string, string> { ["zone"] = "dmz" }, "SSH", "prod", "critical", "active"));

        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>
            {
                new("T1", "New Name", "SSH", "staging", "critical", "active")
            });

        var svc = CreateService();
        await svc.SyncAsync(CancellationToken.None);

        var t = _targetStore.GetById("T1")!;
        t.Name.Should().Be("New Name");
        t.Environment.Should().Be("staging");
        t.Host.Should().Be("10.0.0.1");
        t.Port.Should().Be(22);
        t.Labels!["zone"].Should().Be("dmz");
    }

    [Fact]
    public async Task SyncAsync_EmptyCmdb_LeavesLocalTargets()
    {
        _targetStore.AddOrUpdate(new TargetSystem("T1", "Local", null, null, null, "SSH", "prod", "critical", "active"));

        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>());

        var svc = CreateService();
        await svc.SyncAsync(CancellationToken.None);

        _targetStore.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public async Task SyncAsync_WritesAuditEvent()
    {
        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>
            {
                new("T1", "Server A", "SSH", "prod", "critical", "active")
            });

        var svc = CreateService();
        await svc.SyncAsync(CancellationToken.None);

        _auditStore.GetAll().Should().ContainSingle(e => e.EventType == "cmdb.sync");
    }

    [Fact]
    public async Task SyncAsync_LogsConflicts_WhenLocalTargetAbsentFromCmdb()
    {
        _targetStore.AddOrUpdate(new TargetSystem("LOCAL-1", "OnlyLocal", null, null, null, "SSH", "prod", "critical", "active"));

        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CmdbTarget>
            {
                new("T1", "CMDB Only", "SSH", "prod", "critical", "active")
            });

        var svc = CreateService();
        await svc.SyncAsync(CancellationToken.None);

        _targetStore.GetAll().Should().HaveCount(2);
        var auditEvent = _auditStore.GetAll().First(e => e.EventType == "cmdb.sync");
        auditEvent.SourceIp.Should().Contain("conflicts=1");
    }

    [Fact]
    public async Task SyncAsync_HandlesExceptionGracefully()
    {
        _cmdbClient.FetchTargetsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var svc = CreateService();
        var act = () => svc.SyncAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
