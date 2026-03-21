using PamGateway.Agent;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient("PamGateway", client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("Agent:ApiBaseUrl") ?? "http://localhost:8080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
});
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<SessionTracker>();

var app = builder.Build();
var agentOptions = app.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
app.Urls.Add(agentOptions.ListenUrl);

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(agentOptions.KeepaliveIntervalSeconds)
});

app.Map("/ws/agent/sessions/{sessionId}", async (HttpContext context) =>
{
    var opts = context.RequestServices.GetRequiredService<IOptions<AgentOptions>>().Value;
    var tracker = context.RequestServices.GetRequiredService<SessionTracker>();

    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected WebSocket request");
        return;
    }

    if (tracker.ActiveCount >= opts.MaxParallelSessions)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsync($"Max parallel sessions reached ({opts.MaxParallelSessions})");
        return;
    }

    var sessionId = context.Request.RouteValues["sessionId"]?.ToString() ?? "";
    var ticket = context.Request.Query["ticket"].ToString();
    if (opts.VerifyTicket && !string.IsNullOrWhiteSpace(ticket))
    {
        try
        {
            var httpFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
            var http = httpFactory.CreateClient("PamGateway");
            var resp = await http.GetAsync($"/api/v1/agents/{opts.AgentId}/sessions/{sessionId}/verify-ticket?ticket={Uri.EscapeDataString(ticket)}", context.RequestAborted);
            if (!resp.IsSuccessStatusCode)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Invalid or expired ticket");
                return;
            }
        }
        catch
        {
            // If API not reachable, allow session to proceed (fail-open for connectivity)
        }
    }

    var targetHost = context.Request.Query["targetHost"].ToString();
    var targetPortRaw = context.Request.Query["targetPort"].ToString();
    if (string.IsNullOrWhiteSpace(targetHost) || !int.TryParse(targetPortRaw, out var targetPort))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Missing targetHost/targetPort");
        return;
    }

    tracker.Increment();
    try
    {
        using var idleTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var idleTimeout = TimeSpan.FromSeconds(opts.IdleTimeoutSeconds);

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(targetHost, targetPort, idleTimeoutCts.Token);
        await using var tcpStream = tcpClient.GetStream();

        long lastActivityTicks = DateTime.UtcNow.Ticks;

        var wsToTcp = Task.Run(async () =>
        {
            var buffer = new byte[8192];
            while (!idleTimeoutCts.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), idleTimeoutCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
                {
                    await tcpStream.WriteAsync(buffer.AsMemory(0, result.Count), idleTimeoutCts.Token);
                    Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);
                }
            }
        }, idleTimeoutCts.Token);

        var tcpToWs = Task.Run(async () =>
        {
            var buffer = new byte[8192];
            while (!idleTimeoutCts.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var read = await tcpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), idleTimeoutCts.Token);
                if (read == 0)
                    break;
                await socket.SendAsync(
                    new ArraySegment<byte>(buffer, 0, read),
                    WebSocketMessageType.Binary, true, idleTimeoutCts.Token);
                Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);
            }
        }, idleTimeoutCts.Token);

        var idleWatcher = Task.Run(async () =>
        {
            while (!idleTimeoutCts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), idleTimeoutCts.Token);
                var last = new DateTime(Interlocked.Read(ref lastActivityTicks), DateTimeKind.Utc);
                if (DateTime.UtcNow - last > idleTimeout)
                {
                    idleTimeoutCts.Cancel();
                    break;
                }
            }
        }, idleTimeoutCts.Token);

        try
        {
            await Task.WhenAny(wsToTcp, tcpToWs, idleWatcher);
        }
        catch (OperationCanceledException) { }

        if (socket.State == WebSocketState.Open)
        {
            try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None); }
            catch { /* best effort */ }
        }
    }
    finally
    {
        tracker.Decrement();
    }
});

app.Run();

public sealed class SessionTracker
{
    private int _activeCount;
    public int ActiveCount => _activeCount;
    public void Increment() => Interlocked.Increment(ref _activeCount);
    public void Decrement() => Interlocked.Decrement(ref _activeCount);
}
