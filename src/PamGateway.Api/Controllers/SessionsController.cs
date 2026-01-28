using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/sessions")]
[Authorize]
public sealed class SessionsController : ControllerBase
{
    private readonly ISessionStore _sessions;
    private readonly IAccessRequestStore _requests;
    private readonly ITargetStore _targets;
    private readonly IAuditStore _audit;
    private readonly IAgentStore _agents;
    private readonly IAgentTicketStore _tickets;
    private readonly AccessPolicyEvaluator _policyEvaluator;

    public SessionsController(
        ISessionStore sessions,
        IAccessRequestStore requests,
        ITargetStore targets,
        IAuditStore audit,
        IAgentStore agents,
        IAgentTicketStore tickets,
        AccessPolicyEvaluator policyEvaluator)
    {
        _sessions = sessions;
        _requests = requests;
        _targets = targets;
        _audit = audit;
        _agents = agents;
        _tickets = tickets;
        _policyEvaluator = policyEvaluator;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_sessions.GetAll());

    [HttpPost]
    public IActionResult Create([FromBody] SessionCreateDto dto)
    {
        var request = _requests.GetById(dto.RequestId);
        if (request is null || request.Status != AccessRequestStatus.Approved)
        {
            return Conflict(new { message = "No approved access request" });
        }

        if (request.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Conflict(new { message = "Access request expired" });
        }

        var target = _targets.GetById(dto.TargetId);
        if (target is null)
        {
            return NotFound(new { message = "Target not found" });
        }

        if (!_policyEvaluator.IsSessionAllowed(User, target, dto.Protocol, out var denyReason))
        {
            return Forbid(denyReason);
        }

        var session = new Session(
            $"SES-{Guid.NewGuid():N}",
            dto.TargetId,
            dto.RequestId,
            dto.Protocol,
            SessionStatus.Active,
            DateTimeOffset.UtcNow,
            null
        );

        _sessions.Add(session);
        _audit.Add(AuditEventFactory.Create(HttpContext, "session.started", "connect", "success", dto.TargetId, target.Name, dto.RequestId, session.Id));

        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var session = _sessions.GetById(id);
        if (session is null)
        {
            return NotFound(new { message = "Session not found" });
        }

        return Ok(session);
    }

    [HttpPost("{id}/terminate")]
    public IActionResult Terminate(string id)
    {
        var session = _sessions.GetById(id);
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

        var target = _targets.GetById(session.TargetId);
        _audit.Add(AuditEventFactory.Create(HttpContext, "session.ended", "terminate", "success", session.TargetId, target?.Name ?? "", session.RequestId, session.Id));

        return Ok(updated);
    }

    [HttpPost("{id}/ticket")]
    public IActionResult IssueTicket(string id, [FromBody] SessionTicketIssueDto dto)
    {
        var session = _sessions.GetById(id);
        if (session is null)
        {
            return NotFound(new { message = "Session not found" });
        }

        if (session.Status != SessionStatus.Active)
        {
            return Conflict(new { message = "Session is not active" });
        }

        var agent = _agents.GetById(dto.AgentId);
        if (agent is null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        if (agent.Status != AgentStatus.Online)
        {
            return Conflict(new { message = "Agent is not online" });
        }

        var seconds = dto.ExpiresInSeconds ?? 300;
        if (seconds < 30)
        {
            seconds = 30;
        }
        if (seconds > 3600)
        {
            seconds = 3600;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
        var ticket = _tickets.Issue(session.Id, dto.AgentId, expiresAt);

        return Ok(new
        {
            ticket = ticket.Ticket,
            sessionId = session.Id,
            agentId = dto.AgentId,
            expiresAt = ticket.ExpiresAt
        });
    }
}
