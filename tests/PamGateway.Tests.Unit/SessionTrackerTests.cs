using FluentAssertions;

namespace PamGateway.Tests.Unit;

public sealed class SessionTrackerTests
{
    [Fact]
    public void InitialCount_IsZero()
    {
        var tracker = new SessionTracker();
        tracker.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Increment_IncreasesCount()
    {
        var tracker = new SessionTracker();
        tracker.Increment();
        tracker.Increment();
        tracker.ActiveCount.Should().Be(2);
    }

    [Fact]
    public void Decrement_DecreasesCount()
    {
        var tracker = new SessionTracker();
        tracker.Increment();
        tracker.Increment();
        tracker.Decrement();
        tracker.ActiveCount.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAccess_IsThreadSafe()
    {
        var tracker = new SessionTracker();
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => { tracker.Increment(); }));
        await Task.WhenAll(tasks);

        tracker.ActiveCount.Should().Be(100);

        var decTasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => { tracker.Decrement(); }));
        await Task.WhenAll(decTasks);

        tracker.ActiveCount.Should().Be(50);
    }
}
