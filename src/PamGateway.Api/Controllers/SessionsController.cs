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

    public SessionsController(
        ISessionStore sessions,
        IAccessRequestStore requests,
        ITargetStore targets,
        IAuditStore audit)
    {
        _sessions = sessions;
        _requests = requests;
        _targets = targets;
        _audit = audit;
    }

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
}
