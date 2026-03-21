using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class FullAccessCycleTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public FullAccessCycleTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        EnsurePolicy();

        factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ItsmTicket("PAM-CYCLE", "https://jira.test/browse/PAM-CYCLE"));
    }

    private void EnsurePolicy()
    {
        using var scope = _factory.Services.CreateScope();
        var policyStore = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        if (policyStore.GetById("cycle-allow") is null)
        {
            policyStore.Add(new Policy("cycle-allow", "AllowLinux", "Linux", "*", "Allow",
                new Dictionary<string, string> { ["env"] = "prod" }));
        }
    }

    private async Task<string> CreateTarget()
    {
        var id = $"TGT-{Guid.NewGuid():N}";
        var resp = await _client.PostAsJsonAsync("/api/v1/targets", new
        {
            id,
            name = "CycleTestTarget",
            host = "10.0.0.1",
            port = 22,
            type = "Linux",
            environment = "prod",
            criticality = "high",
            status = "active",
            labels = new Dictionary<string, string> { ["env"] = "prod" }
        });
        resp.EnsureSuccessStatusCode();
        return id;
    }

    private async Task<(string Id, string Status)> CreateRequest(string targetId)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/access/requests", new
        {
            targetId,
            durationMinutes = 60,
            reason = "Integration test"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return (doc.RootElement.GetProperty("id").GetString()!,
                doc.RootElement.GetProperty("status").GetString()!);
    }

    [Fact]
    public async Task FullCycle_CreateRequest_Approve_CreateSession_Terminate()
    {
        var targetId = await CreateTarget();
        var (requestId, status) = await CreateRequest(targetId);
        status.Should().Be("Pending");

        var approveResp = await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var reqCheck = await _client.GetAsync($"/api/v1/access/requests/{requestId}");
        var reqBody = JsonDocument.Parse(await reqCheck.Content.ReadAsStringAsync());
        reqBody.RootElement.GetProperty("status").GetString().Should().Be("Approved");

        var sessionResp = await _client.PostAsJsonAsync("/api/v1/sessions", new
        {
            targetId,
            requestId,
            protocol = "ssh"
        });
        sessionResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var sessionDoc = JsonDocument.Parse(await sessionResp.Content.ReadAsStringAsync());
        var sessionId = sessionDoc.RootElement.GetProperty("id").GetString()!;
        sessionDoc.RootElement.GetProperty("status").GetString().Should().Be("Active");

        var terminateResp = await _client.PostAsync($"/api/v1/sessions/{sessionId}/terminate", null);
        terminateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionCheck = await _client.GetAsync($"/api/v1/sessions/{sessionId}");
        var finalSession = JsonDocument.Parse(await sessionCheck.Content.ReadAsStringAsync());
        finalSession.RootElement.GetProperty("status").GetString().Should().Be("Terminated");
    }

    [Fact]
    public async Task FullCycle_CreateRequest_Deny()
    {
        var targetId = await CreateTarget();
        var (requestId, _) = await CreateRequest(targetId);

        var denyResp = await _client.PostAsync($"/api/v1/access/requests/{requestId}/deny", null);
        denyResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var reqCheck = await _client.GetAsync($"/api/v1/access/requests/{requestId}");
        var reqBody = JsonDocument.Parse(await reqCheck.Content.ReadAsStringAsync());
        reqBody.RootElement.GetProperty("status").GetString().Should().Be("Denied");

        var approvalCheck = await _client.GetAsync("/api/v1/approvals");
        approvalCheck.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WebhookStatusUpdate_ApprovedViaJira()
    {
        var targetId = await CreateTarget();
        var (requestId, _) = await CreateRequest(targetId);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        var req = store.GetById(requestId)!;
        store.Update(req with { ItsmKey = "PAM-999" });

        var webhookResp = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook", new
        {
            issue = new
            {
                key = "PAM-999",
                fields = new
                {
                    status = new { name = "Approved" }
                }
            }
        });
        webhookResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var check = store.GetById(requestId)!;
        check.Status.Should().Be(AccessRequestStatus.Approved);
    }

    [Fact]
    public async Task WebhookStatusUpdate_CancelledMapsToDenied()
    {
        var targetId = await CreateTarget();
        var (requestId, _) = await CreateRequest(targetId);

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IAccessRequestStore>();
        var req = store.GetById(requestId)!;
        store.Update(req with { ItsmKey = "PAM-1000" });

        var webhookResp = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook", new
        {
            issue = new
            {
                key = "PAM-1000",
                fields = new
                {
                    status = new { name = "Cancelled" }
                }
            }
        });
        webhookResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var check = store.GetById(requestId)!;
        check.Status.Should().Be(AccessRequestStatus.Denied);
    }

    [Fact]
    public async Task WebhookStatusUpdate_UnknownKey_Returns404()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/integrations/jira/webhook", new
        {
            issue = new
            {
                key = "UNKNOWN-999",
                fields = new
                {
                    status = new { name = "Approved" }
                }
            }
        });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuditTrail_CapturesAccessEvents()
    {
        var targetId = await CreateTarget();
        var (requestId, _) = await CreateRequest(targetId);

        await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);

        var auditResp = await _client.GetAsync("/api/v1/audit/events");
        auditResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditBody = await auditResp.Content.ReadAsStringAsync();
        auditBody.Should().Contain("access.");
    }
}
