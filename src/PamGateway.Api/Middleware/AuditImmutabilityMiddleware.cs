namespace PamGateway.Api.Middleware;

public sealed class AuditImmutabilityMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> ForbiddenMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "PUT", "PATCH", "DELETE"
    };

    public AuditImmutabilityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/v1/audit", StringComparison.OrdinalIgnoreCase)
            && ForbiddenMethods.Contains(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                status = 405,
                title = "Method Not Allowed",
                detail = "Audit records are immutable. Modification and deletion are prohibited."
            });
            return;
        }

        await _next(context);
    }
}
