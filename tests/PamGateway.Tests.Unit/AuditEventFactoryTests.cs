using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class AuditEventFactoryTests
{
    private static HttpContext CreateHttpContext(
        string? userId = null,
        string? username = null,
        string? role = null,
        string? ipAddress = null)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (username is not null)
            claims.Add(new Claim(ClaimTypes.Name, username));
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        if (ipAddress is not null)
            context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);

        return context;
    }

    [Fact]
    public void Create_WithFullClaims_PopulatesAllFields()
    {
        var context = CreateHttpContext("user-1", "admin", "PAM_Administrator", "192.168.1.10");

        var audit = AuditEventFactory.Create(
            context, "access.requested", "request", "success",
            "T1", "Server", "REQ-1", "S1");

        audit.EventType.Should().Be("access.requested");
        audit.Action.Should().Be("request");
        audit.Result.Should().Be("success");
        audit.UserId.Should().Be("user-1");
        audit.Username.Should().Be("admin");
        audit.Role.Should().Be("PAM_Administrator");
        audit.TargetId.Should().Be("T1");
        audit.TargetName.Should().Be("Server");
        audit.RequestId.Should().Be("REQ-1");
        audit.SessionId.Should().Be("S1");
        audit.SourceIp.Should().Be("192.168.1.10");
        audit.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNoClaims_UsesDefaults()
    {
        var context = new DefaultHttpContext();

        var audit = AuditEventFactory.Create(context, "session.started", "start", "ok");

        audit.UserId.Should().Be("unknown");
        audit.Username.Should().Be("unknown");
        audit.Role.Should().Be("unknown");
    }

    [Fact]
    public void Create_WithSubClaim_FallsBackToSub()
    {
        var claims = new List<Claim> { new("sub", "sub-user-id") };
        var identity = new ClaimsIdentity(claims, "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var audit = AuditEventFactory.Create(context, "test", "action", "ok");

        audit.UserId.Should().Be("sub-user-id");
    }

    [Fact]
    public void Create_WithPreferredUsername_FallsBackToPreferredUsername()
    {
        var claims = new List<Claim> { new("preferred_username", "jdoe") };
        var identity = new ClaimsIdentity(claims, "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var audit = AuditEventFactory.Create(context, "test", "action", "ok");

        audit.Username.Should().Be("jdoe");
    }

    [Fact]
    public void Create_WithNoRemoteIp_ReturnsUnknown()
    {
        var context = new DefaultHttpContext();

        var audit = AuditEventFactory.Create(context, "test", "action", "ok");

        audit.SourceIp.Should().Be("unknown");
    }

    [Fact]
    public void Create_WithDefaultOptionalParams_UsesEmptyStrings()
    {
        var context = CreateHttpContext("u1", "admin", "Admin", "127.0.0.1");

        var audit = AuditEventFactory.Create(context, "test", "action", "ok");

        audit.TargetId.Should().BeEmpty();
        audit.TargetName.Should().BeEmpty();
        audit.RequestId.Should().BeEmpty();
        audit.SessionId.Should().BeEmpty();
    }
}
