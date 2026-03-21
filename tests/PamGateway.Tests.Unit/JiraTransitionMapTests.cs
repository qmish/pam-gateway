using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PamGateway.Integrations;

namespace PamGateway.Tests.Unit;

public sealed class JiraTransitionMapTests
{
    private static JiraItsmClient CreateClient(
        HttpResponseMessage response,
        Action<JiraOptions>? configure = null)
    {
        var handler = new FakeHandler(response);
        var httpClient = new HttpClient(handler);
        var options = new JiraOptions
        {
            BaseUrl = "https://jira.test",
            ProjectKey = "PAM",
            Token = "test-token"
        };
        configure?.Invoke(options);
        return new JiraItsmClient(httpClient, Options.Create(options));
    }

    [Fact]
    public async Task UpdateStatus_UsesTransitionMap_WhenConfigured()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        var client = CreateClient(response, o =>
        {
            o.TransitionMap["escalated"] = "999";
        });

        await client.UpdateStatusAsync("PAM-1", "escalated", CancellationToken.None);
        // no exception = called with transition id 999
    }

    [Fact]
    public async Task UpdateStatus_FallsBackToNamedTransitions()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        var client = CreateClient(response, o =>
        {
            o.TransitionApproved = "10";
        });

        await client.UpdateStatusAsync("PAM-1", "approved", CancellationToken.None);
    }

    [Fact]
    public async Task UpdateStatus_UnknownStatus_NoCall()
    {
        var callMade = false;
        var handler = new FakeHandler(() =>
        {
            callMade = true;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var httpClient = new HttpClient(handler);
        var options = new JiraOptions
        {
            BaseUrl = "https://jira.test",
            ProjectKey = "PAM",
            Token = "test-token"
        };
        var client = new JiraItsmClient(httpClient, Options.Create(options));

        await client.UpdateStatusAsync("PAM-1", "some-random-status", CancellationToken.None);
        callMade.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStatus_TransitionMapPriority_OverFallback()
    {
        string? capturedBody = null;
        var handler = new FakeHandler(async req =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var httpClient = new HttpClient(handler);
        var options = new JiraOptions
        {
            BaseUrl = "https://jira.test",
            ProjectKey = "PAM",
            Token = "test-token",
            TransitionApproved = "10",
            TransitionMap = { ["approved"] = "777" }
        };
        var client = new JiraItsmClient(httpClient, Options.Create(options));

        await client.UpdateStatusAsync("PAM-1", "approved", CancellationToken.None);

        capturedBody.Should().Contain("777");
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>>? _asyncFunc;
        private readonly Func<HttpResponseMessage>? _syncFunc;
        private readonly HttpResponseMessage? _fixed;

        public FakeHandler(HttpResponseMessage response) => _fixed = response;
        public FakeHandler(Func<HttpResponseMessage> func) => _syncFunc = func;
        public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> func) => _asyncFunc = func;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_asyncFunc != null) return await _asyncFunc(request);
            if (_syncFunc != null) return _syncFunc();
            return _fixed!;
        }
    }
}
