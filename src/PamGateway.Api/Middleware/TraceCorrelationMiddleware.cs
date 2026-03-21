using System.Diagnostics;

namespace PamGateway.Api.Middleware;

public sealed class TraceCorrelationMiddleware
{
    private readonly RequestDelegate _next;

    public TraceCorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        if (activity is not null)
        {
            context.Response.Headers["X-Trace-Id"] = activity.TraceId.ToString();
            context.Response.Headers["X-Span-Id"] = activity.SpanId.ToString();

            if (activity.ParentSpanId != default)
                context.Response.Headers["X-Parent-Span-Id"] = activity.ParentSpanId.ToString();
        }

        await _next(context);
    }
}
