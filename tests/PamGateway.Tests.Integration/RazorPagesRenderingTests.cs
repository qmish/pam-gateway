using System.Net;
using FluentAssertions;

namespace PamGateway.Tests.Integration;

public sealed class RazorPagesRenderingTests : IClassFixture<PamUiFactory>
{
    private readonly HttpClient _client;

    public RazorPagesRenderingTests(PamUiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Index")]
    [InlineData("/Targets")]
    [InlineData("/Policies")]
    [InlineData("/Roles")]
    [InlineData("/Agents")]
    [InlineData("/Sessions")]
    [InlineData("/Recordings")]
    [InlineData("/AccessRequests")]
    [InlineData("/Approvals")]
    [InlineData("/ApproverPanel")]
    [InlineData("/Dashboard")]
    [InlineData("/Privacy")]
    public async Task Page_Returns200_WithHtml(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("<!DOCTYPE html>", "page should return full HTML document");
        content.Should().Contain("PAM Gateway", "layout should contain the app brand");
    }

    [Fact]
    public async Task IndexPage_ContainsNavigationCards()
    {
        var html = await _client.GetStringAsync("/");

        html.Should().Contain("Targets");
        html.Should().Contain("Policies");
        html.Should().Contain("Agents");
        html.Should().Contain("Sessions");
        html.Should().Contain("Access Requests");
    }

    [Fact]
    public async Task TargetsPage_ContainsTableAndFilterForm()
    {
        var html = await _client.GetStringAsync("/Targets");

        html.Should().Contain("<table");
        html.Should().Contain("Server-1");
        html.Should().Contain("form");
    }

    [Fact]
    public async Task AgentsPage_ContainsDashboardCards()
    {
        var html = await _client.GetStringAsync("/Agents");

        html.Should().Contain("Agent Dashboard");
        html.Should().Contain("Online");
        html.Should().Contain("Offline");
        html.Should().Contain("agent-01");
    }

    [Fact]
    public async Task PoliciesPage_ContainsTable()
    {
        var html = await _client.GetStringAsync("/Policies");

        html.Should().Contain("<table");
        html.Should().Contain("AllowSSH");
    }

    [Fact]
    public async Task RolesPage_ContainsTable()
    {
        var html = await _client.GetStringAsync("/Roles");

        html.Should().Contain("<table");
        html.Should().Contain("System_Admin_Linux");
    }

    [Fact]
    public async Task SessionsPage_ContainsTable()
    {
        var html = await _client.GetStringAsync("/Sessions");

        html.Should().Contain("<table");
    }

    [Fact]
    public async Task RecordingsPage_ContainsTable()
    {
        var html = await _client.GetStringAsync("/Recordings");

        html.Should().Contain("<table");
    }

    [Fact]
    public async Task AccessRequestsPage_ContainsTableAndStatusBar()
    {
        var html = await _client.GetStringAsync("/AccessRequests");

        html.Should().Contain("<table");
        html.Should().Contain("Pending");
    }

    [Fact]
    public async Task ApprovalsPage_ContainsTable()
    {
        var html = await _client.GetStringAsync("/Approvals");

        html.Should().Contain("<table");
    }

    [Fact]
    public async Task ApproverPanelPage_ContainsApproverElements()
    {
        var html = await _client.GetStringAsync("/ApproverPanel");

        html.Should().Contain("Approver");
    }

    [Fact]
    public async Task DashboardPage_ContainsOverviewCards()
    {
        var html = await _client.GetStringAsync("/Dashboard");

        html.Should().Contain("Обзор системы");
        html.Should().Contain("Online");
        html.Should().Contain("Активные сессии");
    }

    [Fact]
    public async Task HealthzEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("ok");
    }

    [Fact]
    public async Task NonExistentPage_Returns404()
    {
        var response = await _client.GetAsync("/this-page-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AllPages_ContainNavigation()
    {
        var pages = new[] { "/", "/Targets", "/Agents", "/Sessions", "/AccessRequests" };
        foreach (var page in pages)
        {
            var html = await _client.GetStringAsync(page);
            html.Should().Contain("navbar", $"{page} should include the navigation bar");
        }
    }

    [Fact]
    public async Task Layout_ContainsBootstrapCss()
    {
        var html = await _client.GetStringAsync("/");

        html.Should().Contain("bootstrap");
    }

    [Fact]
    public async Task Layout_ContainsThemeToggle()
    {
        var html = await _client.GetStringAsync("/");

        html.Should().Contain("theme-toggle");
        html.Should().Contain("theme.js");
    }

    [Fact]
    public async Task Layout_ContainsLangToggle()
    {
        var html = await _client.GetStringAsync("/");

        html.Should().Contain("lang-toggle");
    }

    [Fact]
    public async Task Layout_ContainsDashboardNavLink()
    {
        var html = await _client.GetStringAsync("/");

        html.Should().Contain("Dashboard");
    }
}
