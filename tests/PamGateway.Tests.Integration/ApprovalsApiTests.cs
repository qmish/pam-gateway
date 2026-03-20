using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Integration;

public sealed class ApprovalsApiTests : IClassFixture<PamApiFactory>
{
    private readonly HttpClient _client;

    public ApprovalsApiTests(PamApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/approvals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var approvals = await response.Content.ReadFromJsonAsync<Approval[]>();
        approvals.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_ReturnsOk_WithApproval()
    {
        var dto = new ApprovalCreateDto("REQ-1", "admin", "approved");
        var response = await _client.PostAsJsonAsync("/api/v1/approvals", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var approval = await response.Content.ReadFromJsonAsync<Approval>();
        approval.Should().NotBeNull();
        approval!.RequestId.Should().Be("REQ-1");
        approval.Approver.Should().Be("admin");
        approval.Status.Should().Be("approved");
        approval.Id.Should().StartWith("APR-");
    }

    [Fact]
    public async Task Create_DeniedStatus_ReturnsOk()
    {
        var dto = new ApprovalCreateDto("REQ-2", "security", "denied");
        var response = await _client.PostAsJsonAsync("/api/v1/approvals", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var approval = await response.Content.ReadFromJsonAsync<Approval>();
        approval!.Status.Should().Be("denied");
    }

    [Fact]
    public async Task GetAll_AfterCreate_ContainsNewApproval()
    {
        var dto = new ApprovalCreateDto("REQ-GETALL", "admin", "approved");
        await _client.PostAsJsonAsync("/api/v1/approvals", dto);

        var response = await _client.GetAsync("/api/v1/approvals");
        var approvals = await response.Content.ReadFromJsonAsync<Approval[]>();

        approvals.Should().Contain(a => a.RequestId == "REQ-GETALL");
    }
}
