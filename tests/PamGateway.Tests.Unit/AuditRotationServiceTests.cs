using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api.Services;
using PamGateway.Data;

namespace PamGateway.Tests.Unit;

public sealed class AuditRotationServiceTests : IDisposable
{
    private readonly string _dbName = $"AuditRotation_{Guid.NewGuid()}";
    private readonly ServiceProvider _sp;

    public AuditRotationServiceTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PamGatewayDbContext>(opts =>
            opts.UseInMemoryDatabase(_dbName));
        _sp = services.BuildServiceProvider();
    }

    public void Dispose() => _sp.Dispose();

    private PamGatewayDbContext GetDb()
    {
        var scope = _sp.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PamGatewayDbContext>();
    }

    private AuditRotationService CreateService(AuditRotationOptions? opts = null)
    {
        opts ??= new AuditRotationOptions { Enabled = true, RetentionDays = 30, BatchSize = 100 };
        return new AuditRotationService(
            _sp,
            Options.Create(opts),
            Substitute.For<ILogger<AuditRotationService>>());
    }

    [Fact]
    public async Task RotateAsync_DeletesOldEvents()
    {
        using (var db = GetDb())
        {
            db.AuditEvents.AddRange(
                new AuditEventEntity
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-60),
                    EventType = "old.event", UserId = "u1", Username = "user1",
                    Role = "admin", TargetId = "t1", TargetName = "srv1",
                    Action = "test", Result = "ok", RequestId = "", SessionId = "", SourceIp = ""
                },
                new AuditEventEntity
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
                    EventType = "recent.event", UserId = "u2", Username = "user2",
                    Role = "admin", TargetId = "t2", TargetName = "srv2",
                    Action = "test", Result = "ok", RequestId = "", SessionId = "", SourceIp = ""
                });
            await db.SaveChangesAsync();
        }

        var svc = CreateService(new AuditRotationOptions { Enabled = true, RetentionDays = 30, BatchSize = 100 });
        var deleted = await svc.RotateAsync(CancellationToken.None);

        deleted.Should().Be(1);
        using var checkDb = GetDb();
        checkDb.AuditEvents.Should().ContainSingle(e => e.EventType == "recent.event");
    }

    [Fact]
    public async Task RotateAsync_RespectsRetentionPeriod()
    {
        using (var db = GetDb())
        {
            db.AuditEvents.Add(new AuditEventEntity
            {
                Timestamp = DateTimeOffset.UtcNow.AddDays(-5),
                EventType = "fresh.event", UserId = "u1", Username = "user1",
                Role = "admin", TargetId = "t1", TargetName = "srv1",
                Action = "test", Result = "ok", RequestId = "", SessionId = "", SourceIp = ""
            });
            await db.SaveChangesAsync();
        }

        var svc = CreateService(new AuditRotationOptions { Enabled = true, RetentionDays = 30, BatchSize = 100 });
        var deleted = await svc.RotateAsync(CancellationToken.None);

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task RotateAsync_HandlesEmptyTable()
    {
        var svc = CreateService();
        var deleted = await svc.RotateAsync(CancellationToken.None);
        deleted.Should().Be(0);
    }

    [Fact]
    public async Task RotateAsync_BatchDeletion()
    {
        using (var db = GetDb())
        {
            for (int i = 0; i < 5; i++)
            {
                db.AuditEvents.Add(new AuditEventEntity
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-100),
                    EventType = $"old.{i}", UserId = "u", Username = "u",
                    Role = "r", TargetId = "t", TargetName = "s",
                    Action = "a", Result = "ok", RequestId = "", SessionId = "", SourceIp = ""
                });
            }
            await db.SaveChangesAsync();
        }

        var svc = CreateService(new AuditRotationOptions { Enabled = true, RetentionDays = 30, BatchSize = 2 });
        var deleted = await svc.RotateAsync(CancellationToken.None);

        deleted.Should().Be(5);
        using var checkDb = GetDb();
        checkDb.AuditEvents.Should().BeEmpty();
    }
}
