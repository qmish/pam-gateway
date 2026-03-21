using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PamGateway.Api.Services;

namespace PamGateway.Tests.Unit;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task Notify_SendsWebhookRequest()
    {
        string? capturedBody = null;
        string? capturedUrl = null;
        var handler = new FakeHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var opts = Options.Create(new NotificationOptions
        {
            Enabled = true,
            WebhookUrl = "https://hooks.test/pam",
            WebhookSecret = "secret123",
            Events = new() { "access.approved" }
        });

        var svc = new WebhookNotificationService(httpClient, opts,
            Substitute.For<ILogger<WebhookNotificationService>>());

        await svc.NotifyAsync("access.approved", new { requestId = "REQ-1" });

        capturedUrl.Should().Be("https://hooks.test/pam");
        capturedBody.Should().Contain("access.approved");
        capturedBody.Should().Contain("REQ-1");
    }

    [Fact]
    public async Task Notify_SkipsDisabledEvents()
    {
        var called = false;
        var handler = new FakeHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var opts = Options.Create(new NotificationOptions
        {
            Enabled = true,
            WebhookUrl = "https://hooks.test/pam",
            Events = new() { "access.approved" }
        });

        var svc = new WebhookNotificationService(httpClient, opts,
            Substitute.For<ILogger<WebhookNotificationService>>());

        await svc.NotifyAsync("session.started", new { sessionId = "S-1" });
        called.Should().BeFalse();
    }

    [Fact]
    public async Task Notify_SkipsWhenDisabled()
    {
        var called = false;
        var handler = new FakeHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var opts = Options.Create(new NotificationOptions { Enabled = false });

        var svc = new WebhookNotificationService(httpClient, opts,
            Substitute.For<ILogger<WebhookNotificationService>>());

        await svc.NotifyAsync("access.approved", new { requestId = "REQ-1" });
        called.Should().BeFalse();
    }

    [Fact]
    public async Task Notify_IncludesSecret()
    {
        string? secretHeader = null;
        var handler = new FakeHandler(req =>
        {
            if (req.Headers.TryGetValues("X-Pam-Webhook-Secret", out var vals))
                secretHeader = vals.FirstOrDefault();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var opts = Options.Create(new NotificationOptions
        {
            Enabled = true,
            WebhookUrl = "https://hooks.test/pam",
            WebhookSecret = "my-secret",
            Events = new() { "access.denied" }
        });

        var svc = new WebhookNotificationService(httpClient, opts,
            Substitute.For<ILogger<WebhookNotificationService>>());

        await svc.NotifyAsync("access.denied", new { });
        secretHeader.Should().Be("my-secret");
    }

    [Fact]
    public async Task Notify_DoesNotThrowOnError()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("fail"));
        var httpClient = new HttpClient(handler);
        var opts = Options.Create(new NotificationOptions
        {
            Enabled = true,
            WebhookUrl = "https://hooks.test/pam",
            Events = new() { "access.approved" }
        });

        var svc = new WebhookNotificationService(httpClient, opts,
            Substitute.For<ILogger<WebhookNotificationService>>());

        var act = () => svc.NotifyAsync("access.approved", new { });
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NoopService_DoesNothing()
    {
        var svc = new NoopNotificationService();
        await svc.NotifyAsync("anything", new { });
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _func;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> func) => _func = func;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_func(request));
    }
}
