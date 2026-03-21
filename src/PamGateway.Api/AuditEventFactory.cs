using System.Security.Claims;
using PamGateway.Core;

namespace PamGateway.Api;

public static class AuditEventFactory
{
    public static AuditEvent Create(
        HttpContext context,
        string eventType,
        string action,
        string result,
        string targetId = "",
        string targetName = "",
        string requestId = "",
        string sessionId = "")
    {
        var user = context.User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? "unknown";
        var username = user.Identity?.Name ?? user.FindFirstValue("preferred_username") ?? "unknown";
        var role = user.FindFirstValue(ClaimTypes.Role) ?? "unknown";
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();

        return new AuditEvent(
            DateTimeOffset.UtcNow,
            eventType,
            userId,
            username,
            role,
            targetId,
            targetName,
            action,
            result,
            requestId,
            sessionId,
            sourceIp,
            userAgent
        );
    }
}
