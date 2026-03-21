using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;
using Microsoft.Extensions.Options;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/agents")]
[Authorize]
public sealed class AgentsController : ControllerBase
{
    private readonly IAgentStore _agents;
    private readonly IAgentTicketStore _tickets;
    private readonly ISessionStore _sessions;
    private readonly ITargetStore _targets;
    private readonly AgentApiOptions _options;

    public AgentsController(
        IAgentStore agents,
        IAgentTicketStore tickets,
        ISessionStore sessions,
        ITargetStore targets,
        IOptions<AgentApiOptions> options)
    {
        _agents = agents;
        _tickets = tickets;
        _sessions = sessions;
        _targets = targets;
        _options = options.Value;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_agents.GetAll());

    [HttpPost("register")]
    public IActionResult Register([FromBody] AgentRegisterDto dto)
    {
        if (!IsJoinTokenValid(dto.JoinToken))
        {
            return Unauthorized(new { message = "Invalid join token" });
        }

        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        var labels = dto.Labels ?? new Dictionary<string, string>();
        var capabilities = dto.Capabilities ?? new List<string>();
        var agent = new AgentInfo(
            dto.AgentId,
            dto.Hostname,
            dto.Os,
            AgentStatus.Online,
            now,
            dto.PublicUrl ?? string.Empty,
            labels,
            capabilities,
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
        var agent = _agents.GetById(dto.AgentId);
        if (agent is null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        if (!IsAgentTokenValid(agent))
        {
            return Unauthorized(new { message = "Invalid agent token" });
        }

        var status = Enum.TryParse<AgentStatus>(dto.Status, true, out var parsed)
            ? parsed
            : AgentStatus.Online;

        var updated = _agents.UpdateHeartbeat(dto.AgentId, DateTimeOffset.UtcNow, status);
        return Ok(new { ok = true, status = updated.Status.ToString().ToLowerInvariant() });
    }

    [HttpPost("{agentId}/sessions")]
    public IActionResult StartSession(string agentId, [FromBody] AgentSessionCreateDto dto)
    {
        var agent = _agents.GetById(agentId);
        if (agent is null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        if (!IsAgentTokenValid(agent))
        {
            return Unauthorized(new { message = "Invalid agent token" });
        }

        if (agent.Status != AgentStatus.Online)
        {
            return Conflict(new { message = "Agent is not online" });
        }

        var ticket = _tickets.GetByTicket(dto.Ticket);
        if (ticket is null || ticket.AgentId != agentId || ticket.SessionId != dto.SessionId)
        {
            return Unauthorized(new { message = "Invalid session ticket" });
        }

        if (ticket.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Unauthorized(new { message = "Session ticket expired" });
        }

        var session = _sessions.GetById(dto.SessionId);
        if (session is null)
        {
            return NotFound(new { message = "Session not found" });
        }

        if (session.Status == SessionStatus.Terminated)
        {
            return Conflict(new { message = "Session already terminated" });
        }

        _tickets.Revoke(dto.Ticket);

        var target = _targets.GetById(dto.TargetId);
        if (string.IsNullOrWhiteSpace(agent.PublicUrl))
        {
            return Conflict(new { message = "Agent publicUrl is not configured" });
        }

        if (string.IsNullOrWhiteSpace(target?.Host) || target?.Port is null)
        {
            return Conflict(new { message = "Target host/port is not configured" });
        }

        var baseUrl = agent.PublicUrl.TrimEnd('/');
        var tunnelUrl = $"{baseUrl}/ws/agent/sessions/{session.Id}?targetHost={Uri.EscapeDataString(target.Host)}&targetPort={target.Port}";

        return CreatedAtAction(nameof(StartSession), new { agentId }, new
        {
            sessionId = session.Id,
            status = session.Status.ToString().ToLowerInvariant(),
            targetName = target?.Name,
            proxyTunnelUrl = tunnelUrl
        });
    }

    [HttpGet("{agentId}/sessions/{sessionId}/verify-ticket")]
    [AllowAnonymous]
    public IActionResult VerifyTicket(string agentId, string sessionId, [FromQuery] string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
            return BadRequest(new { message = "ticket is required" });

        var stored = _tickets.GetByTicket(ticket);
        if (stored is null || stored.AgentId != agentId || stored.SessionId != sessionId)
            return Unauthorized(new { message = "Invalid ticket" });

        if (stored.ExpiresAt <= DateTimeOffset.UtcNow)
            return Unauthorized(new { message = "Ticket expired" });

        return Ok(new { valid = true });
    }

    [HttpPost("{agentId}/sessions/{sessionId}/terminate")]
    public IActionResult TerminateSession(string agentId, string sessionId, [FromBody] AgentSessionTerminateDto dto)
    {
        var agent = _agents.GetById(agentId);
        if (agent is null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        if (!IsAgentTokenValid(agent))
        {
            return Unauthorized(new { message = "Invalid agent token" });
        }

        var session = _sessions.GetById(sessionId);
        if (session is null)
        {
            return NotFound(new { message = "Session not found" });
        }

        if (session.Status == SessionStatus.Terminated)
        {
            return Conflict(new { message = "Session already terminated" });
        }

        var updated = session with { Status = SessionStatus.Terminated, EndedAt = DateTimeOffset.UtcNow };
        _sessions.Update(updated);

        return Ok(new { status = "terminated", endedAt = updated.EndedAt, reason = dto.Reason });
    }

    private bool IsJoinTokenValid(string? joinToken)
    {
        if (string.IsNullOrWhiteSpace(_options.JoinToken))
        {
            return true;
        }

        return string.Equals(_options.JoinToken, joinToken ?? string.Empty, StringComparison.Ordinal);
    }

    private bool IsAgentTokenValid(AgentInfo agent)
    {
        if (!_options.RequireAgentToken)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(agent.Token))
        {
            return false;
        }

        var token = GetAgentToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return string.Equals(agent.Token, token, StringComparison.Ordinal);
    }

    private string? GetAgentToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader["Bearer ".Length..].Trim();
            }

            return authHeader.Trim();
        }

        if (Request.Headers.TryGetValue("X-Agent-Token", out var values))
        {
            var token = values.ToString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token.Trim();
            }
        }

        return null;
    }
}
