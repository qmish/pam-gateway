using PamGateway.Agent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient("PamGateway", client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("Agent:ApiBaseUrl") ?? "http://localhost:8080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
