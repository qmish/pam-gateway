using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Api.Controllers;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Tests.Unit;

public sealed class JiraTransitionsExtendedTests
{
    private readonly InMemoryAccessRequestStore _requests = new();
    private readonly InMemoryTargetStore _targets = new(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    private readonly InMemoryAuditStore _audit = new();
    private readonly InMemoryApprovalStore _approvals = new();

    private JiraWebhooksController CreateController(JiraOptions? opts = null)
    {
        var options = opts ?? new JiraOptions();
        var controller = new JiraWebhooksController(
            _requests, _targets, _approvals, _audit,
            Options.Create(options),
            Substitute.For<ILogger<JiraWebhooksController>>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private void SeedRequest(string id, string itsmKey, AccessRequestStatus status = AccessRequestStatus.Pending)
    {
        _requests.Add(new AccessRequest(id, "T1", "user1", 60, "reason",
            status, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), itsmKey));
    }

    private static JiraIssueWebhookDto Webhook(string issueKey, string status) => new(
        "issue_updated", new JiraIssueDto("123", issueKey, new JiraIssueFieldsDto(new JiraStatusDto(status))));

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Canceled")]
    public void CancelledStatus_MapsToDenied(string cancelStatus)
    {
        SeedRequest("REQ-C", "JIRA-C1");
        var controller = CreateController();
        var result = controller.HandleWebhook(Webhook("JIRA-C1", cancelStatus)) as OkObjectResult;
        result.Should().NotBeNull();
        _requests.GetById("REQ-C")!.Status.Should().Be(AccessRequestStatus.Denied);
    }

    [Theory]
    [InlineData("Reopened")]
    [InlineData("In Progress")]
    [InlineData("Open")]
    public void ReopenedStatus_MapsToPending(string reopenStatus)
    {
        SeedRequest("REQ-R", "JIRA-R1", AccessRequestStatus.Denied);
        var controller = CreateController();
        var result = controller.HandleWebhook(Webhook("JIRA-R1", reopenStatus)) as OkObjectResult;
        result.Should().NotBeNull();
        _requests.GetById("REQ-R")!.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public void ConfiguredStatusMap_Overrides()
    {
        SeedRequest("REQ-M", "JIRA-M1");
        var opts = new JiraOptions
        {
            StatusMap = new() { ["CustomDone"] = "Approved" }
        };
        var controller = CreateController(opts);
        var result = controller.HandleWebhook(Webhook("JIRA-M1", "CustomDone")) as OkObjectResult;
        result.Should().NotBeNull();
        _requests.GetById("REQ-M")!.Status.Should().Be(AccessRequestStatus.Approved);
    }

    [Fact]
    public void UnknownStatus_IgnoredGracefully()
    {
        SeedRequest("REQ-U", "JIRA-U1");
        var controller = CreateController();
        var result = controller.HandleWebhook(Webhook("JIRA-U1", "SomeWeirdStatus")) as OkObjectResult;
        result.Should().NotBeNull();
        _requests.GetById("REQ-U")!.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public void DuplicateStatus_Ignored()
    {
        SeedRequest("REQ-D", "JIRA-D1", AccessRequestStatus.Approved);
        var controller = CreateController();
        var result = controller.HandleWebhook(Webhook("JIRA-D1", "Approved")) as OkObjectResult;
        result.Should().NotBeNull();
        _audit.GetAll().Should().NotContain(e => e.EventType == "access.status.sync");
    }

    [Fact]
    public void CancelledCreatesApproval()
    {
        SeedRequest("REQ-CA", "JIRA-CA1");
        var controller = CreateController();
        controller.HandleWebhook(Webhook("JIRA-CA1", "Cancelled"));
        _approvals.GetAll().Should().ContainSingle(a => a.RequestId == "REQ-CA" && a.Status == "denied");
    }

    [Fact]
    public void MissingIssueKey_ReturnsBadRequest()
    {
        var controller = CreateController();
        var payload = new JiraIssueWebhookDto("issue_updated",
            new JiraIssueDto("123", null, new JiraIssueFieldsDto(new JiraStatusDto("Approved"))));
        var result = controller.HandleWebhook(payload);
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
