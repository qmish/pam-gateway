using PamGateway.Agent;
using Microsoft.Extensions.Options;
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

var app = builder.Build();
var agentOptions = app.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
app.Urls.Add(agentOptions.ListenUrl);

app.UseWebSockets();
app.Map("/ws/agent/sessions/{sessionId}", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected WebSocket request");
        return;
    }

    var targetHost = context.Request.Query["targetHost"].ToString();
    var targetPortRaw = context.Request.Query["targetPort"].ToString();
    if (string.IsNullOrWhiteSpace(targetHost) || !int.TryParse(targetPortRaw, out var targetPort))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Missing targetHost/targetPort");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    using var tcpClient = new TcpClient();
    await tcpClient.ConnectAsync(targetHost, targetPort, context.RequestAborted);
    await using var tcpStream = tcpClient.GetStream();

    var wsToTcp = Task.Run(async () =>
    {
        var buffer = new byte[8192];
        while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
            {
                await tcpStream.WriteAsync(buffer.AsMemory(0, result.Count), context.RequestAborted);
            }
        }
    }, context.RequestAborted);

    var tcpToWs = Task.Run(async () =>
    {
        var buffer = new byte[8192];
        while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var read = await tcpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), context.RequestAborted);
            if (read == 0)
            {
                break;
            }

            await socket.SendAsync(
                new ArraySegment<byte>(buffer, 0, read),
                WebSocketMessageType.Binary,
                true,
                context.RequestAborted);
        }
    }, context.RequestAborted);

    await Task.WhenAny(wsToTcp, tcpToWs);
    if (socket.State == WebSocketState.Open)
    {
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", context.RequestAborted);
    }
});

app.Run();
