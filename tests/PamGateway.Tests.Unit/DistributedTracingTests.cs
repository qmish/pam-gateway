using System.Diagnostics;
using FluentAssertions;

namespace PamGateway.Tests.Unit;

public sealed class DistributedTracingTests
{
    [Fact]
    public void ApiActivitySource_CanCreateActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "PamGateway.Api",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var source = new ActivitySource("PamGateway.Api");
        using var activity = source.StartActivity("TestOperation");

        activity.Should().NotBeNull();
        activity!.Source.Name.Should().Be("PamGateway.Api");
        activity.OperationName.Should().Be("TestOperation");
    }

    [Fact]
    public void WorkerActivitySource_CanCreateActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "PamGateway.Worker",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var source = new ActivitySource("PamGateway.Worker");
        using var activity = source.StartActivity("WorkerCycle");

        activity.Should().NotBeNull();
        activity!.Source.Name.Should().Be("PamGateway.Worker");
    }

    [Fact]
    public void ActivityContext_PropagatesTraceId()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var source = new ActivitySource("PamGateway.Api");
        using var parent = source.StartActivity("ParentOp");
        parent.Should().NotBeNull();

        using var child = source.StartActivity("ChildOp");
        child.Should().NotBeNull();
        child!.ParentId.Should().NotBeNullOrWhiteSpace();
        child.TraceId.Should().Be(parent!.TraceId);
    }

    [Fact]
    public void Activity_SupportsTagsForCorrelation()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var source = new ActivitySource("PamGateway.Api");
        using var activity = source.StartActivity("ProcessRequest");
        activity!.SetTag("request.id", "REQ-123");
        activity.SetTag("target.id", "TGT-456");

        activity.GetTagItem("request.id").Should().Be("REQ-123");
        activity.GetTagItem("target.id").Should().Be("TGT-456");
    }
}
