using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class JiraWebhookApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public JiraWebhookApiTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Webhook_MissingIssueKey_ReturnsBadRequest()
    {
        var payload = new JiraIssueWebhookDto("issue_updated", new JiraIssueDto(null, null, null));
        var response = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook", payload);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_RequestNotFound_Returns404()
    {
        var payload = new JiraIssueWebhookDto("issue_updated",
            new JiraIssueDto("1", "UNKNOWN-KEY",
                new JiraIssueFieldsDto(new JiraStatusDto("Approved"))));

        var response = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook", payload);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_ApprovedStatus_UpdatesRequest()
    {
        var targetType = $"WHType_{Guid.NewGuid():N}"[..16];
        var targetId = $"WH-T-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/targets",
            new TargetUpsertDto(targetId, "WHTarget", "10.0.0.1", 22, null, targetType, "prod", "critical", "active"));
        await _client.PostAsJsonAsync("/api/v1/policies",
            new PolicyCreateDto($"Pol-{targetType}", targetType, "ssh", "Allow", null));

        _factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("WH-JIRA-1", "url"));

        await _client.PostAsJsonAsync("/api/v1/access/requests",
            new AccessRequestCreateDto(targetId, 60, "webhook test"));

        var payload = new JiraIssueWebhookDto("issue_updated",
            new JiraIssueDto("1", "WH-JIRA-1",
                new JiraIssueFieldsDto(new JiraStatusDto("Approved"))));

        var response = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("approved");
    }
}
