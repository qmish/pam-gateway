using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PamGateway.Core;
using PamGateway.Integrations;
using PamGateway.Worker;

namespace PamGateway.Tests.Unit;

public sealed class DeadLetterQueueTests
{
    [Fact]
    public void Add_And_GetPending()
    {
        var store = new InMemoryDeadLetterStore();
        store.Add(new DeadLetterItem("DLQ-1", "update_status", "JIRA-1", "expired",
            "Connection refused", DateTimeOffset.UtcNow, 0, null, false));
        store.Add(new DeadLetterItem("DLQ-2", "add_comment", "JIRA-2", "test comment",
            "Timeout", DateTimeOffset.UtcNow, 0, null, false));

        store.GetPending().Should().HaveCount(2);
        store.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void MarkResolved_ExcludesFromPending()
    {
        var store = new InMemoryDeadLetterStore();
        store.Add(new DeadLetterItem("DLQ-R", "update_status", "JIRA-1", "expired",
            "err", DateTimeOffset.UtcNow, 0, null, false));

        store.MarkResolved("DLQ-R");
        store.GetPending().Should().BeEmpty();
        store.GetAll().Should().ContainSingle(x => x.Resolved);
    }

    [Fact]
    public void IncrementRetry_UpdatesCount()
    {
        var store = new InMemoryDeadLetterStore();
        store.Add(new DeadLetterItem("DLQ-I", "update_status", "JIRA-1", "expired",
            "err", DateTimeOffset.UtcNow, 0, null, false));

        store.IncrementRetry("DLQ-I");
        store.IncrementRetry("DLQ-I");

        var item = store.GetAll().First(x => x.Id == "DLQ-I");
        item.RetryCount.Should().Be(2);
        item.LastRetryAt.Should().NotBeNull();
    }

    [Fact]
    public void GetPending_RespectsLimit()
    {
        var store = new InMemoryDeadLetterStore();
        for (int i = 0; i < 10; i++)
            store.Add(new DeadLetterItem($"DLQ-L{i}", "update_status", $"J-{i}", "x",
                "err", DateTimeOffset.UtcNow, 0, null, false));

        store.GetPending(3).Should().HaveCount(3);
    }

    [Fact]
    public async Task Processor_ResolvesSuccessfulItems()
    {
        var dlq = new InMemoryDeadLetterStore();
        dlq.Add(new DeadLetterItem("P-1", "update_status", "JIRA-10", "expired",
            "err", DateTimeOffset.UtcNow, 0, null, false));

        var itsm = Substitute.For<IItsmClient>();
        var services = new ServiceCollection();
        services.AddSingleton<IDeadLetterStore>(dlq);
        services.AddSingleton(itsm);
        var provider = services.BuildServiceProvider();

        var processor = new DeadLetterProcessor(
            Substitute.For<ILogger<DeadLetterProcessor>>(), provider);

        var resolved = await processor.ProcessPendingItems(CancellationToken.None);
        resolved.Should().Be(1);
        dlq.GetPending().Should().BeEmpty();
    }

    [Fact]
    public async Task Processor_IncrementsRetryOnFailure()
    {
        var dlq = new InMemoryDeadLetterStore();
        dlq.Add(new DeadLetterItem("P-2", "update_status", "JIRA-F", "expired",
            "err", DateTimeOffset.UtcNow, 0, null, false));

        var itsm = Substitute.For<IItsmClient>();
        itsm.UpdateStatusAsync("JIRA-F", "expired", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("fail"));

        var services = new ServiceCollection();
        services.AddSingleton<IDeadLetterStore>(dlq);
        services.AddSingleton(itsm);
        var provider = services.BuildServiceProvider();

        var processor = new DeadLetterProcessor(
            Substitute.For<ILogger<DeadLetterProcessor>>(), provider);

        await processor.ProcessPendingItems(CancellationToken.None);
        dlq.GetPending().Should().ContainSingle(x => x.RetryCount == 1);
    }

    [Fact]
    public async Task Processor_MarksResolvedAfterMaxRetries()
    {
        var dlq = new InMemoryDeadLetterStore();
        dlq.Add(new DeadLetterItem("P-3", "update_status", "JIRA-MAX", "expired",
            "err", DateTimeOffset.UtcNow, 10, null, false));

        var itsm = Substitute.For<IItsmClient>();
        var services = new ServiceCollection();
        services.AddSingleton<IDeadLetterStore>(dlq);
        services.AddSingleton(itsm);
        var provider = services.BuildServiceProvider();

        var processor = new DeadLetterProcessor(
            Substitute.For<ILogger<DeadLetterProcessor>>(), provider);

        await processor.ProcessPendingItems(CancellationToken.None);
        dlq.GetPending().Should().BeEmpty();
    }

    [Fact]
    public async Task Processor_HandlesCommentOperation()
    {
        var dlq = new InMemoryDeadLetterStore();
        dlq.Add(new DeadLetterItem("P-C", "add_comment", "JIRA-C1", "Hello comment",
            "err", DateTimeOffset.UtcNow, 0, null, false));

        var itsm = Substitute.For<IItsmClient>();
        var services = new ServiceCollection();
        services.AddSingleton<IDeadLetterStore>(dlq);
        services.AddSingleton(itsm);
        var provider = services.BuildServiceProvider();

        var processor = new DeadLetterProcessor(
            Substitute.For<ILogger<DeadLetterProcessor>>(), provider);

        await processor.ProcessPendingItems(CancellationToken.None);
        await itsm.Received(1).AddCommentAsync("JIRA-C1", "Hello comment", Arg.Any<CancellationToken>());
    }
}
