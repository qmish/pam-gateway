using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PamGateway.Api.Middleware;

namespace PamGateway.Tests.Unit;

public sealed class TraceCorrelationMiddlewareTests
{
    [Fact]
    public async Task InjectsTraceHeaders_WhenActivityPresent()
    {
        var source = new ActivitySource("test-source");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test-op");
        Activity.Current = activity;

        var context = new DefaultHttpContext();
        var middleware = new TraceCorrelationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers.Should().ContainKey("X-Trace-Id");
        context.Response.Headers.Should().ContainKey("X-Span-Id");
        context.Response.Headers["X-Trace-Id"].ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NoHeaders_WhenNoActivity()
    {
        Activity.Current = null;
        var context = new DefaultHttpContext();
        var middleware = new TraceCorrelationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Trace-Id").Should().BeFalse();
    }
}
