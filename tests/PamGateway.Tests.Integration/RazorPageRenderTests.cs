using System.Net;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class RazorPageRenderTests : IClassFixture<PamUiFactory>
{
    private readonly HttpClient _client;

    public RazorPageRenderTests(PamUiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Targets")]
    [InlineData("/Agents")]
    [InlineData("/Roles")]
    [InlineData("/Policies")]
    [InlineData("/Sessions")]
    [InlineData("/Recordings")]
    [InlineData("/AccessRequests")]
    [InlineData("/Approvals")]
    [InlineData("/ApproverPanel")]
    public async Task Page_Returns200_AndContainsHtml(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("<!DOCTYPE html>", "page should return full HTML document");
    }

    [Fact]
    public async Task TargetsPage_ContainsTargetData()
    {
        var html = await _client.GetStringAsync("/Targets");

        html.Should().Contain("Server-1");
        html.Should().Contain("10.0.0.1");
        html.Should().Contain("Linux Server");
    }

    [Fact]
    public async Task AgentsPage_ContainsAgentData()
    {
        var html = await _client.GetStringAsync("/Agents");

        html.Should().Contain("agent-01");
        html.Should().Contain("Online");
        html.Should().Contain("linux");
    }

    [Fact]
    public async Task RolesPage_ContainsRoleData()
    {
        var html = await _client.GetStringAsync("/Roles");

        html.Should().Contain("System_Admin_Linux");
    }

    [Fact]
    public async Task PoliciesPage_ContainsPolicyData()
    {
        var html = await _client.GetStringAsync("/Policies");

        html.Should().Contain("AllowSSH");
        html.Should().Contain("ssh");
    }

    [Fact]
    public async Task SessionsPage_ContainsSessionData()
    {
        var html = await _client.GetStringAsync("/Sessions");

        html.Should().Contain("S1");
        html.Should().Contain("Active");
    }

    [Fact]
    public async Task RecordingsPage_ContainsRecordingData()
    {
        var html = await _client.GetStringAsync("/Recordings");

        html.Should().Contain("REC-1");
        html.Should().Contain("Completed");
    }

    [Fact]
    public async Task AccessRequestsPage_ContainsRequestData()
    {
        var html = await _client.GetStringAsync("/AccessRequests");

        html.Should().Contain("REQ-1");
        html.Should().Contain("Pending");
        html.Should().Contain("user1");
    }

    [Fact]
    public async Task ApprovalsPage_ContainsApprovalData()
    {
        var html = await _client.GetStringAsync("/Approvals");

        html.Should().Contain("APR-1");
        html.Should().Contain("approved");
    }

    [Fact]
    public async Task ApproverPanelPage_ContainsPendingRequests()
    {
        var html = await _client.GetStringAsync("/ApproverPanel");

        html.Should().Contain("REQ-1");
        html.Should().Contain("Maintenance");
    }

    [Fact]
    public async Task HealthzEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }

    [Fact]
    public async Task TargetsPage_FilterBySearch()
    {
        var html = await _client.GetStringAsync("/Targets?Search=Server");

        html.Should().Contain("Server-1");
    }

    [Fact]
    public async Task TargetsPage_FilterByType()
    {
        var html = await _client.GetStringAsync("/Targets?TypeFilter=Linux+Server");

        html.Should().Contain("Server-1");
    }

    [Fact]
    public async Task NonExistentPage_Returns404()
    {
        var response = await _client.GetAsync("/NonExistent12345");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Layout_ContainsNavigation()
    {
        var html = await _client.GetStringAsync("/");

        html.Should().Contain("Targets");
        html.Should().Contain("Agents");
        html.Should().Contain("Sessions");
    }
}
