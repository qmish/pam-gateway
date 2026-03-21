using FluentAssertions;
using PamGateway.Core;

using SiemService = PamGateway.Api.Services.SiemExportService;

namespace PamGateway.Tests.Unit;

public sealed class SiemExportServiceTests
{
    [Fact]
    public void FormatCefMessage_ProducesValidCef()
    {
        var evt = new AuditEvent(
            new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero),
            "access.requested",
            "user-123",
            "jdoe",
            "System_Admin_Linux",
            "SCH-1",
            "Linux Server",
            "request",
            "pending",
            "REQ-1",
            "",
            "10.0.0.1");

        var cef = SiemService.FormatCefMessage(evt);

        cef.Should().StartWith("CEF:0|PAMGateway|PAM|1.0|");
        cef.Should().Contain("access.requested");
        cef.Should().Contain("src=10.0.0.1");
        cef.Should().Contain("suser=jdoe");
        cef.Should().Contain("dst=SCH-1");
        cef.Should().Contain("outcome=pending");
    }

    [Fact]
    public void FormatCefMessage_DeniedEvent_HasHighSeverity()
    {
        var evt = new AuditEvent(
            DateTimeOffset.UtcNow,
            "access.denied",
            "user-456",
            "admin",
            "PAM_Administrator",
            "SCH-2",
            "AD Server",
            "deny",
            "denied",
            "REQ-2",
            "SESS-1",
            "192.168.1.1");

        var cef = SiemService.FormatCefMessage(evt);

        cef.Should().Contain("|7|", "denied events should have severity 7");
        cef.Should().Contain("outcome=denied");
    }

    [Fact]
    public void FormatCefMessage_SuccessEvent_HasLowSeverity()
    {
        var evt = new AuditEvent(
            DateTimeOffset.UtcNow,
            "session.started",
            "user-789",
            "operator",
            "DevOps",
            "SCH-3",
            "K8s Cluster",
            "start",
            "success",
            "REQ-3",
            "SESS-2",
            "172.16.0.1");

        var cef = SiemService.FormatCefMessage(evt);

        cef.Should().Contain("|3|", "success events should have severity 3");
        cef.Should().Contain("outcome=success");
    }

    [Fact]
    public void FormatCefMessage_IncludesSessionAndRequestIds()
    {
        var evt = new AuditEvent(
            DateTimeOffset.UtcNow,
            "session.ended",
            "u1",
            "user1",
            "Admin",
            "t1",
            "Server1",
            "end",
            "success",
            "REQ-100",
            "SESS-200",
            "10.10.10.10");

        var cef = SiemService.FormatCefMessage(evt);

        cef.Should().Contain("cs1=REQ-100");
        cef.Should().Contain("cs2=SESS-200");
    }
}
