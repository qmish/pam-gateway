using FluentAssertions;
using PamGateway.Api.Services;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class SiemEventTests
{
    [Fact]
    public void FormatCefMessage_ContainsAllFields()
    {
        var evt = new AuditEvent(
            DateTimeOffset.Parse("2026-03-21T12:00:00Z"),
            "access.approved", "user1", "admin", "PAM_Admin",
            "t1", "Server-1", "Approved access", "success",
            "req-1", "sess-1", "10.0.0.1", "Mozilla/5.0");

        var cef = SiemExportService.FormatCefMessage(evt);

        cef.Should().StartWith("CEF:0|PAMGateway|PAM|1.0|");
        cef.Should().Contain("access.approved");
        cef.Should().Contain("src=10.0.0.1");
        cef.Should().Contain("suser=admin");
        cef.Should().Contain("duser=user1");
        cef.Should().Contain("dst=t1");
        cef.Should().Contain("dhost=Server-1");
        cef.Should().Contain("cs1=req-1");
        cef.Should().Contain("cs2=sess-1");
        cef.Should().Contain("outcome=success");
        cef.Should().Contain("requestClientApplication=Mozilla/5.0");
    }

    [Fact]
    public void FormatCefMessage_DeniedEventHasHighSeverity()
    {
        var evt = new AuditEvent(
            DateTimeOffset.UtcNow, "access.denied", "u1", "admin", "role",
            "t1", "Srv", "Deny", "denied", "", "", "1.2.3.4");

        var cef = SiemExportService.FormatCefMessage(evt);
        cef.Should().Contain("|7|", "denied events should have severity 7");
    }

    [Fact]
    public void FormatCefMessage_SuccessEventHasLowSeverity()
    {
        var evt = new AuditEvent(
            DateTimeOffset.UtcNow, "session.started", "u1", "admin", "role",
            "t1", "Srv", "Start session", "success", "", "", "1.2.3.4");

        var cef = SiemExportService.FormatCefMessage(evt);
        cef.Should().Contain("|3|", "success events should have severity 3");
    }

    [Fact]
    public void FormatCefMessage_EscapesSpecialChars()
    {
        var evt = new AuditEvent(
            DateTimeOffset.UtcNow, "test", "u1", "admin", "role",
            "t1", "Srv", "test", "success", "", "", "1.2.3.4",
            "Agent=with|pipe");

        var cef = SiemExportService.FormatCefMessage(evt);
        cef.Should().Contain("Agent\\=with\\|pipe");
    }

    [Fact]
    public void SiemEventTypes_ContainsAllKnownTypes()
    {
        SiemEventTypes.All.Should().Contain("user.login");
        SiemEventTypes.All.Should().Contain("access.requested");
        SiemEventTypes.All.Should().Contain("session.started");
        SiemEventTypes.All.Should().Contain("session.ended");
        SiemEventTypes.All.Should().Contain("policy.violation");
        SiemEventTypes.All.Should().Contain("system.heartbeat");
        SiemEventTypes.All.Should().Contain("vault.credential.checkout");
        SiemEventTypes.All.Should().Contain("vault.breakglass.checkout");
    }

    [Fact]
    public void SiemEventTypes_AllCountIsCorrect()
    {
        SiemEventTypes.All.Should().HaveCount(16);
    }

    [Fact]
    public void AuditEvent_IncludesUserAgent()
    {
        var evt = new AuditEvent(
            DateTimeOffset.UtcNow, "test", "u1", "admin", "role",
            "", "", "test", "ok", "", "", "1.1.1.1", "TestAgent/1.0");

        evt.UserAgent.Should().Be("TestAgent/1.0");
    }
}
