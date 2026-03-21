using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PamGateway.Integrations;

namespace PamGateway.Tests.Unit;

public sealed class JiraCommentTests
{
    [Fact]
    public async Task AddComment_SendsCorrectRequest()
    {
        string? capturedUrl = null;
        string? capturedBody = null;
        var handler = new FakeHandler(async req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var httpClient = new HttpClient(handler);
        var options = new JiraOptions { BaseUrl = "https://jira.test", Token = "t" };
        var client = new JiraItsmClient(httpClient, Options.Create(options));

        await client.AddCommentAsync("PAM-42", "Test comment body", CancellationToken.None);

        capturedUrl.Should().Contain("/rest/api/2/issue/PAM-42/comment");
        capturedBody.Should().Contain("Test comment body");
    }

    [Fact]
    public async Task GetComments_ParsesResponse()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            comments = new[]
            {
                new { id = "1001", author = new { displayName = "Alice" }, body = "First comment", created = "2026-03-21T10:00:00Z" },
                new { id = "1002", author = new { displayName = "Bob" }, body = "Second comment", created = "2026-03-21T11:00:00Z" }
            }
        });

        var handler = new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(handler);
        var options = new JiraOptions { BaseUrl = "https://jira.test", Token = "t" };
        var client = new JiraItsmClient(httpClient, Options.Create(options));

        var comments = await client.GetCommentsAsync("PAM-42", CancellationToken.None);

        comments.Should().HaveCount(2);
        comments[0].Author.Should().Be("Alice");
        comments[0].Body.Should().Be("First comment");
        comments[1].Id.Should().Be("1002");
    }

    [Fact]
    public async Task GetComments_EmptyResponse_ReturnsEmptyList()
    {
        var handler = new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"comments\":[]}", System.Text.Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(handler);
        var options = new JiraOptions { BaseUrl = "https://jira.test", Token = "t" };
        var client = new JiraItsmClient(httpClient, Options.Create(options));

        var comments = await client.GetCommentsAsync("PAM-99", CancellationToken.None);
        comments.Should().BeEmpty();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>>? _asyncFunc;
        private readonly Func<HttpRequestMessage, HttpResponseMessage>? _syncFunc;

        public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> func) => _asyncFunc = func;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> func) => _syncFunc = func;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_asyncFunc != null) return await _asyncFunc(request);
            return _syncFunc!(request);
        }
    }
}
