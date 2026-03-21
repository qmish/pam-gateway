using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class WebhookExtendedTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public WebhookExtendedTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> SeedRequest(string itsmKey)
    {
        var targetType = $"WH-{Guid.NewGuid():N}"[..12];
        var targetId = $"WH-T-{Guid.NewGuid():N}"[..16];

        await _client.PostAsJsonAsync("/api/v1/targets",
            new TargetUpsertDto(targetId, "WhTarget", "10.0.0.1", 22, null,
                targetType, "prod", "critical", "active"));

        using var scope = _factory.Services.CreateScope();
        var policies = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        policies.Add(new Policy($"pol-{targetType}", $"Allow{targetType}", targetType, "*", "Allow", null));

        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket(itsmKey, "https://jira.test"));

        var resp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "webhook test"));
        resp.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> SendWebhook(string issueKey, string statusName)
    {
        return await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook",
            new JiraIssueWebhookDto("issue_updated",
                new JiraIssueDto("1", issueKey,
                    new JiraIssueFieldsDto(new JiraStatusDto(statusName)))));
    }

    [Fact]
    public async Task Webhook_Approved_UpdatesStatus()
    {
        var requestId = await SeedRequest("WH-APR-001");
        var resp = await SendWebhook("WH-APR-001", "Approved");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Approved);
    }

    [Fact]
    public async Task Webhook_Denied_UpdatesStatus()
    {
        var requestId = await SeedRequest("WH-DEN-001");
        var resp = await SendWebhook("WH-DEN-001", "Denied");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Denied);
    }

    [Fact]
    public async Task Webhook_Cancelled_MapsToDenied()
    {
        var requestId = await SeedRequest("WH-CAN-001");
        var resp = await SendWebhook("WH-CAN-001", "Cancelled");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Denied);
    }

    [Fact]
    public async Task Webhook_Canceled_MapsToDenied()
    {
        var requestId = await SeedRequest("WH-CAN-002");
        var resp = await SendWebhook("WH-CAN-002", "Canceled");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Denied);
    }

    [Fact]
    public async Task Webhook_Rejected_MapsToDenied()
    {
        var requestId = await SeedRequest("WH-REJ-001");
        var resp = await SendWebhook("WH-REJ-001", "Rejected");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Denied);
    }

    [Fact]
    public async Task Webhook_Reopened_MapsToPending()
    {
        var requestId = await SeedRequest("WH-ROP-001");

        await SendWebhook("WH-ROP-001", "Approved");

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Approved);

        var reopenResp = await SendWebhook("WH-ROP-001", "Reopened");
        reopenResp.StatusCode.Should().Be(HttpStatusCode.OK);
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public async Task Webhook_InProgress_MapsToPending()
    {
        var requestId = await SeedRequest("WH-IP-001");

        await SendWebhook("WH-IP-001", "Approved");

        var resp = await SendWebhook("WH-IP-001", "In Progress");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        store.GetById(requestId)!.Status.Should().Be(AccessRequestStatus.Pending);
    }

    [Fact]
    public async Task Webhook_UnknownStatus_IsIgnored()
    {
        var requestId = await SeedRequest("WH-UNK-001");
        var resp = await SendWebhook("WH-UNK-001", "SomeRandomStatus");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("ignored");
    }

    [Fact]
    public async Task Webhook_DuplicateStatus_IsIgnored()
    {
        await SeedRequest("WH-DUP-001");
        await SendWebhook("WH-DUP-001", "Approved");
        var resp = await SendWebhook("WH-DUP-001", "Approved");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("ignored");
    }

    [Fact]
    public async Task Webhook_MissingIssueKey_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook",
            new JiraIssueWebhookDto("issue_updated",
                new JiraIssueDto("1", null, new JiraIssueFieldsDto(new JiraStatusDto("Approved")))));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_MissingStatus_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook",
            new JiraIssueWebhookDto("issue_updated",
                new JiraIssueDto("1", "KEY-1", new JiraIssueFieldsDto(null))));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_NullIssue_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook",
            new JiraIssueWebhookDto("issue_updated", null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_UnknownIssueKey_Returns404()
    {
        var resp = await SendWebhook("NONEXISTENT-KEY", "Approved");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_CreatesAuditEvent()
    {
        await SeedRequest("WH-AUD-001");
        await SendWebhook("WH-AUD-001", "Approved");

        using var scope = _factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditStore>();
        audit.GetAll().Should().Contain(e => e.EventType == "access.status.sync");
    }

    [Fact]
    public async Task Webhook_ApprovedCreatesApproval()
    {
        var requestId = await SeedRequest("WH-APRA-001");
        await SendWebhook("WH-APRA-001", "Approved");

        using var scope = _factory.Services.CreateScope();
        var approvals = scope.ServiceProvider.GetRequiredService<IApprovalStore>();
        approvals.GetAll().Should().Contain(a => a.RequestId == requestId && a.Status == "approved");
    }
}
