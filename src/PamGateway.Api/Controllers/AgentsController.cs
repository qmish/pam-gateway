using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/agents")]
[Authorize]
public sealed class AgentsController : ControllerBase
{
    private readonly IAgentStore _agents;

    public AgentsController(IAgentStore agents)
    {
        _agents = agents;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_agents.GetAll());

    [HttpPost("register")]
    public IActionResult Register([FromBody] AgentRegisterDto dto)
    {
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        var agent = new AgentInfo(
            dto.AgentId,
            dto.Hostname,
            dto.Os,
            AgentStatus.Online,
            now,
            dto.Labels,
            dto.Capabilities,
            token);

        _agents.Register(agent);

        return Ok(new
        {
            agentToken = token,
            agentCert = "stub",
            heartbeatIntervalSec = 30
        });
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat([FromBody] AgentHeartbeatDto dto)
    {
        var status = Enum.TryParse<AgentStatus>(dto.Status, true, out var parsed)
            ? parsed
            : AgentStatus.Online;

        var updated = _agents.UpdateHeartbeat(dto.AgentId, DateTimeOffset.UtcNow, status);
        return Ok(new { ok = true, status = updated.Status.ToString().ToLowerInvariant() });
    }
}
