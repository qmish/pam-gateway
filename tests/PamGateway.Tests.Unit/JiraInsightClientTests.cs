using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PamGateway.Integrations;

namespace PamGateway.Tests.Unit;

public sealed class JiraInsightClientTests
{
    private static CmdbOptions DefaultOptions() => new()
    {
        BaseUrl = "https://jira.example.com",
        Iql = "objectType=System",
        AuthType = "Bearer",
        Token = "test-token",
        TypeAttribute = "Тип",
        EnvironmentAttribute = "Среда",
        CriticalityAttribute = "Критичность",
        StatusAttribute = "Статус",
        DefaultType = "Unknown",
        DefaultEnvironment = "prod",
        DefaultCriticality = "non-critical",
        DefaultStatus = "Active"
    };

    private static (JiraInsightClient client, MockHttpHandler handler) CreateClient(CmdbOptions? options = null)
    {
        var opts = options ?? DefaultOptions();
        var handler = new MockHttpHandler();
        var httpClient = new HttpClient(handler);
        var client = new JiraInsightClient(httpClient, Options.Create(opts));
        return (client, handler);
    }

    [Fact]
    public async Task FetchTargetsAsync_ParsesObjectEntries()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "objectEntries": [
                    { "id": 100, "label": "Server A" },
                    { "id": 200, "label": "Server B" }
                ]
            }
            """, Encoding.UTF8, "application/json")
        });

        handler.AddResponse("*/rest/insight/1.0/object/100", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "attributes": [
                    { "objectTypeAttribute": { "name": "Тип" }, "objectAttributeValues": [{ "value": "SSH" }] },
                    { "objectTypeAttribute": { "name": "Среда" }, "objectAttributeValues": [{ "value": "prod" }] },
                    { "objectTypeAttribute": { "name": "Критичность" }, "objectAttributeValues": [{ "value": "critical" }] },
                    { "objectTypeAttribute": { "name": "Статус" }, "objectAttributeValues": [{ "value": "Active" }] }
                ]
            }
            """, Encoding.UTF8, "application/json")
        });

        handler.AddResponse("*/rest/insight/1.0/object/200", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "attributes": [
                    { "objectTypeAttribute": { "name": "Тип" }, "objectAttributeValues": [{ "value": "RDP" }] },
                    { "objectTypeAttribute": { "name": "Среда" }, "objectAttributeValues": [{ "value": "test" }] }
                ]
            }
            """, Encoding.UTF8, "application/json")
        });

        var targets = await client.FetchTargetsAsync(CancellationToken.None);

        targets.Should().HaveCount(2);
        targets[0].Id.Should().Be("100");
        targets[0].Name.Should().Be("Server A");
        targets[0].Type.Should().Be("SSH");
        targets[0].Environment.Should().Be("prod");
        targets[0].Criticality.Should().Be("critical");
        targets[0].Status.Should().Be("Active");

        targets[1].Type.Should().Be("RDP");
        targets[1].Environment.Should().Be("test");
        targets[1].Criticality.Should().Be("non-critical");
        targets[1].Status.Should().Be("Active");
    }

    [Fact]
    public async Task FetchTargetsAsync_EmptyResponse_ReturnsEmpty()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        var targets = await client.FetchTargetsAsync(CancellationToken.None);
        targets.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchTargetsAsync_EmptyObjectEntries_ReturnsEmpty()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "objectEntries": [] }""", Encoding.UTF8, "application/json")
        });

        var targets = await client.FetchTargetsAsync(CancellationToken.None);
        targets.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchTargetsAsync_UsesBasicAuth()
    {
        var opts = DefaultOptions();
        opts.AuthType = "Basic";
        opts.Username = "admin";
        opts.Token = "secret";

        var (client, handler) = CreateClient(opts);

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "objectEntries": [] }""", Encoding.UTF8, "application/json")
        });

        await client.FetchTargetsAsync(CancellationToken.None);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Basic");
    }

    [Fact]
    public async Task FetchTargetsAsync_UsesBearerAuth()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "objectEntries": [] }""", Encoding.UTF8, "application/json")
        });

        await client.FetchTargetsAsync(CancellationToken.None);

        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("test-token");
    }

    [Fact]
    public async Task FetchTargetsAsync_UsesDefaultValues_WhenAttributesMissing()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            { "objectEntries": [{ "id": 1, "label": "Srv" }] }
            """, Encoding.UTF8, "application/json")
        });

        handler.AddResponse("*/rest/insight/1.0/object/1", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "attributes": [] }""", Encoding.UTF8, "application/json")
        });

        var targets = await client.FetchTargetsAsync(CancellationToken.None);

        targets.Should().HaveCount(1);
        targets[0].Type.Should().Be("Unknown");
        targets[0].Environment.Should().Be("prod");
        targets[0].Criticality.Should().Be("non-critical");
        targets[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task FetchTargetsAsync_WithDisplayValue_ExtractsCorrectly()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            { "objectEntries": [{ "id": 5, "label": "DB" }] }
            """, Encoding.UTF8, "application/json")
        });

        handler.AddResponse("*/rest/insight/1.0/object/5", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "attributes": [
                    {
                        "objectTypeAttribute": { "name": "Тип" },
                        "objectAttributeValues": [{ "displayValue": "Database" }]
                    }
                ]
            }
            """, Encoding.UTF8, "application/json")
        });

        var targets = await client.FetchTargetsAsync(CancellationToken.None);
        targets[0].Type.Should().Be("Database");
    }

    [Fact]
    public async Task FetchTargetsAsync_WithReferencedObject_ExtractsLabel()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            { "objectEntries": [{ "id": 7 }] }
            """, Encoding.UTF8, "application/json")
        });

        handler.AddResponse("*/rest/insight/1.0/object/7", new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
                "attributes": [
                    {
                        "objectTypeAttribute": { "name": "Статус" },
                        "objectAttributeValues": [{ "referencedObject": { "label": "Running" } }]
                    }
                ]
            }
            """, Encoding.UTF8, "application/json")
        });

        var targets = await client.FetchTargetsAsync(CancellationToken.None);
        targets[0].Status.Should().Be("Running");
    }

    [Fact]
    public async Task FetchTargetsAsync_ServerError_ThrowsHttpRequestException()
    {
        var (client, handler) = CreateClient();

        handler.AddResponse("*/rest/insight/1.0/object/navlist/iql*",
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => client.FetchTargetsAsync(CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly List<(string pattern, HttpResponseMessage response)> _responses = new();
        public HttpRequestMessage? LastRequest { get; private set; }

        public void AddResponse(string urlPattern, HttpResponseMessage response)
            => _responses.Add((urlPattern, response));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var url = request.RequestUri?.ToString() ?? "";
            foreach (var (pattern, response) in _responses)
            {
                if (MatchesWildcard(url, pattern))
                    return Task.FromResult(response);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static bool MatchesWildcard(string input, string pattern)
        {
            var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
            var pos = 0;
            foreach (var part in parts)
            {
                var idx = input.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return false;
                pos = idx + part.Length;
            }
            return true;
        }
    }
}
