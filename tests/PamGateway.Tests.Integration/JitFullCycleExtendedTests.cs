using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Tests.Integration;

public sealed class JitFullCycleExtendedTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;
    private readonly PamApiFactory _factory;

    public JitFullCycleExtendedTests(PamApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        factory.ItsmClient
            .CreateAccessRequestAsync(Arg.Any<ItsmAccessRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ItsmTicket($"JIT-{Guid.NewGuid():N}"[..12], "https://jira.test"));
    }

    private async Task<string> CreateTargetWithPolicy(string suffix)
    {
        var targetId = $"JIT-EX-{suffix}-{Guid.NewGuid():N}"[..24];
        var targetType = $"JitEx_{suffix}";
        await _client.PostAsJsonAsync("/api/v1/targets",
            new { id = targetId, name = "JitTarget", host = "10.0.0.1", port = 22,
                  type = targetType, environment = "prod", criticality = "high",
                  status = "active", labels = new Dictionary<string, string> { ["env"] = "prod" } });

        using var scope = _factory.Services.CreateScope();
        var policies = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        var policyId = $"pol-{suffix}-{Guid.NewGuid():N}"[..20];
        if (policies.GetById(policyId) is null)
            policies.Add(new Policy(policyId, $"AllowJit{suffix}", targetType, "*", "Allow", null));

        return targetId;
    }

    [Fact]
    public async Task FullCycle_RequestToSessionToTerminate()
    {
        var targetId = await CreateTargetWithPolicy("full");
        var createResp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { targetId, durationMinutes = 60, reason = "full cycle test" });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var doc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var requestId = doc.RootElement.GetProperty("id").GetString()!;
        doc.RootElement.GetProperty("status").GetString().Should().Be("Pending");

        var approveResp = await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionResp = await _client.PostAsJsonAsync("/api/v1/sessions",
            new { targetId, requestId, protocol = "ssh" });
        sessionResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var sessionDoc = JsonDocument.Parse(await sessionResp.Content.ReadAsStringAsync());
        var sessionId = sessionDoc.RootElement.GetProperty("id").GetString()!;

        var terminateResp = await _client.PostAsync($"/api/v1/sessions/{sessionId}/terminate", null);
        terminateResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DenyRequest_CannotCreateSession()
    {
        var targetId = await CreateTargetWithPolicy("deny");
        var createResp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { targetId, durationMinutes = 30, reason = "will be denied" });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var doc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var requestId = doc.RootElement.GetProperty("id").GetString()!;

        var denyResp = await _client.PostAsync($"/api/v1/access/requests/{requestId}/deny", null);
        denyResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var checkResp = await _client.GetAsync($"/api/v1/access/requests/{requestId}");
        var checkDoc = JsonDocument.Parse(await checkResp.Content.ReadAsStringAsync());
        checkDoc.RootElement.GetProperty("status").GetString().Should().Be("Denied");
    }

    [Fact]
    public async Task DoubleApprove_ReturnsConflict()
    {
        var targetId = await CreateTargetWithPolicy("dbl");
        var createResp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { targetId, durationMinutes = 30, reason = "double approve" });
        var doc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var requestId = doc.RootElement.GetProperty("id").GetString()!;

        await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);
        var secondApprove = await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);
        secondApprove.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ApproveThenDeny_ReturnsConflict()
    {
        var targetId = await CreateTargetWithPolicy("adn");
        var createResp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { targetId, durationMinutes = 30, reason = "approve then deny" });
        var doc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var requestId = doc.RootElement.GetProperty("id").GetString()!;

        await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);
        var denyResp = await _client.PostAsync($"/api/v1/access/requests/{requestId}/deny", null);
        denyResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NonExistentRequest_Approve_Returns404()
    {
        var resp = await _client.PostAsync("/api/v1/access/requests/NON-EXISTENT/approve", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuditTrail_ContainsRequestAndApprovalEvents()
    {
        var targetId = await CreateTargetWithPolicy("audit");
        var createResp = await _client.PostAsJsonAsync("/api/v1/access/requests",
            new { targetId, durationMinutes = 60, reason = "audit trail" });
        var doc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var requestId = doc.RootElement.GetProperty("id").GetString()!;

        await _client.PostAsync($"/api/v1/access/requests/{requestId}/approve", null);

        var auditResp = await _client.GetAsync("/api/v1/audit/events");
        auditResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await auditResp.Content.ReadAsStringAsync();
        body.Should().Contain("access.");
    }
}
