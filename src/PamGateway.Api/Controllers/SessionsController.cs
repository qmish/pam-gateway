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
    private readonly ICredentialStore _credentials;
    private readonly ICredentialCheckoutStore _checkouts;
    private readonly AccessPolicyEvaluator _policyEvaluator;

    public SessionsController(
        ISessionStore sessions,
        IAccessRequestStore requests,
        ITargetStore targets,
        IAuditStore audit,
        IAgentStore agents,
        IAgentTicketStore tickets,
        ICredentialStore credentials,
        ICredentialCheckoutStore checkouts,
        AccessPolicyEvaluator policyEvaluator)
    {
        _sessions = sessions;
        _requests = requests;
        _targets = targets;
        _audit = audit;
        _agents = agents;
        _tickets = tickets;
        _credentials = credentials;
        _checkouts = checkouts;
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

        string? injectedCredentialId = null;
        var availableCred = _credentials.GetByTargetId(dto.TargetId)
            .FirstOrDefault(c => c.Status == CredentialStatus.Available && !c.IsBreakGlass);

        if (availableCred is not null)
        {
            var username = User.Identity?.Name ?? "unknown";
            var checkout = new CredentialCheckout(
                $"CO-{Guid.NewGuid():N}",
                availableCred.Id, username, DateTimeOffset.UtcNow, null,
                $"Auto-injected for session on {target.Name}"
            );
            _checkouts.Add(checkout);
            _credentials.Update(availableCred with
            {
                Status = CredentialStatus.CheckedOut,
                LastCheckedOutAt = DateTimeOffset.UtcNow,
                CheckedOutBy = username
            });
            injectedCredentialId = availableCred.Id;

            _audit.Add(AuditEventFactory.Create(HttpContext, "vault.credential.injected",
                $"Auto-injected {availableCred.Username}@{target.Name}", "success",
                targetId: dto.TargetId));
        }

        var session = new Session(
            $"SES-{Guid.NewGuid():N}",
            dto.TargetId,
            dto.RequestId,
            dto.Protocol,
            SessionStatus.Active,
            DateTimeOffset.UtcNow,
            null,
            injectedCredentialId
        );

        _sessions.Add(session);
        _audit.Add(AuditEventFactory.Create(HttpContext, "session.started", "connect", "success", dto.TargetId, target.Name, dto.RequestId, session.Id));

        return CreatedAtAction(nameof(GetById), new { id = session.Id }, new
        {
            session.Id,
            session.TargetId,
            session.RequestId,
            session.Protocol,
            session.Status,
            session.StartedAt,
            session.EndedAt,
            credentialInjected = injectedCredentialId is not null
        });
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

    [HttpGet("{id}/credentials")]
    public IActionResult GetInjectedCredentials(string id)
    {
        var session = _sessions.GetById(id);
        if (session is null)
            return NotFound(new { message = "Session not found" });

        if (session.Status != SessionStatus.Active)
            return Conflict(new { message = "Session is not active" });

        if (session.InjectedCredentialId is null)
            return Ok(new { injected = false });

        var cred = _credentials.GetById(session.InjectedCredentialId);
        if (cred is null)
            return Ok(new { injected = false });

        var password = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(cred.EncryptedPassword));

        _audit.Add(AuditEventFactory.Create(HttpContext, "vault.credential.retrieved",
            $"Agent retrieved injected credential for session {id}", "success",
            targetId: session.TargetId, sessionId: id));

        return Ok(new { injected = true, username = cred.Username, password });
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

        if (session.InjectedCredentialId is not null)
        {
            CheckinCredential(session.InjectedCredentialId, session.TargetId);
        }

        var updated = session with { Status = SessionStatus.Terminated, EndedAt = DateTimeOffset.UtcNow };
        _sessions.Update(updated);

        var target = _targets.GetById(session.TargetId);
        _audit.Add(AuditEventFactory.Create(HttpContext, "session.ended", "terminate", "success", session.TargetId, target?.Name ?? "", session.RequestId, session.Id));

        return Ok(updated);
    }

    private void CheckinCredential(string credentialId, string targetId)
    {
        var cred = _credentials.GetById(credentialId);
        if (cred is null || cred.Status != CredentialStatus.CheckedOut) return;

        var activeCheckout = _checkouts.GetByCredentialId(credentialId)
            .FirstOrDefault(c => c.CheckedInAt is null);
        if (activeCheckout is not null)
            _checkouts.Update(activeCheckout with { CheckedInAt = DateTimeOffset.UtcNow });

        _credentials.Update(cred with { Status = CredentialStatus.Available, CheckedOutBy = null });
        _audit.Add(AuditEventFactory.Create(HttpContext, "vault.credential.checkin",
            $"Auto-checkin {cred.Username} on session terminate", "success",
            targetId: targetId));
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
