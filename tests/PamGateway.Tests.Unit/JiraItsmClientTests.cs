using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PamGateway.Integrations;

namespace PamGateway.Tests.Unit;

public sealed class JiraItsmClientTests
{
    private static JiraOptions DefaultOptions => new()
    {
        BaseUrl = "https://jira.example.com",
        ProjectKey = "PAM",
        IssueType = "Task",
        AuthType = "Bearer",
        Token = "test-token",
        TransitionApproved = "31",
        TransitionDenied = "41",
        TransitionExpired = "51",
        TransitionPending = "21"
    };

    private static (JiraItsmClient client, MockHttpMessageHandler handler) CreateClient(JiraOptions? options = null)
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var client = new JiraItsmClient(httpClient, Options.Create(options ?? DefaultOptions));
        return (client, handler);
    }

    [Fact]
    public async Task CreateAccessRequest_SendsCorrectPayload()
    {
        var (client, handler) = CreateClient();
        handler.SetupResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { key = "PAM-123" }));

        var result = await client.CreateAccessRequestAsync(
            new ItsmAccessRequest("Summary", "Description", "user1", "T1", "60"),
            CancellationToken.None);

        result.Key.Should().Be("PAM-123");
        result.Url.Should().Contain("PAM-123");
        handler.LastRequestUri.Should().Contain("/rest/api/2/issue");
        handler.LastRequestMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task CreateAccessRequest_BearerAuth()
    {
        var (client, handler) = CreateClient();
        handler.SetupResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { key = "PAM-1" }));

        await client.CreateAccessRequestAsync(
            new ItsmAccessRequest("S", "D", "u", "T", "60"),
            CancellationToken.None);

        handler.LastAuthHeader.Should().Contain("Bearer test-token");
    }

    [Fact]
    public async Task CreateAccessRequest_BasicAuth()
    {
        var opts = DefaultOptions;
        opts.AuthType = "Basic";
        opts.Username = "admin";
        opts.Token = "secret";
        var (client, handler) = CreateClient(opts);
        handler.SetupResponse(HttpStatusCode.Created,
            JsonSerializer.Serialize(new { key = "PAM-1" }));

        await client.CreateAccessRequestAsync(
            new ItsmAccessRequest("S", "D", "u", "T", "60"),
            CancellationToken.None);

        handler.LastAuthHeader.Should().StartWith("Basic ");
    }

    [Fact]
    public async Task UpdateStatus_WithKnownTransition_SendsRequest()
    {
        var (client, handler) = CreateClient();
        handler.SetupResponse(HttpStatusCode.NoContent, "");

        await client.UpdateStatusAsync("PAM-100", "approved", CancellationToken.None);

        handler.LastRequestUri.Should().Contain("/rest/api/2/issue/PAM-100/transitions");
    }

    [Fact]
    public async Task UpdateStatus_UnknownStatus_DoesNotSendRequest()
    {
        var (client, handler) = CreateClient();
        handler.SetupResponse(HttpStatusCode.NoContent, "");

        await client.UpdateStatusAsync("PAM-100", "unknown_status", CancellationToken.None);

        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAccessRequest_ServerError_Throws()
    {
        var (client, handler) = CreateClient();
        handler.SetupResponse(HttpStatusCode.InternalServerError, "");

        var act = () => client.CreateAccessRequestAsync(
            new ItsmAccessRequest("S", "D", "u", "T", "60"),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private string _responseContent = "";

    public string? LastRequestUri { get; private set; }
    public HttpMethod? LastRequestMethod { get; private set; }
    public string? LastAuthHeader { get; private set; }
    public int RequestCount { get; private set; }

    public void SetupResponse(HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _responseContent = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri?.ToString();
        LastRequestMethod = request.Method;
        LastAuthHeader = request.Headers.Authorization?.ToString();

        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent)
        });
    }
}
