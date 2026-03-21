using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/access/requests")]
[Authorize]
public sealed class AccessRequestsController : ControllerBase
{
    private readonly IAccessRequestStore _store;
    private readonly ITargetStore _targets;
    private readonly IAuditStore _audit;
    private readonly IApprovalStore _approvals;
    private readonly IItsmClient _itsmClient;
    private readonly AccessPolicyEvaluator _policyEvaluator;
    private readonly IPolicyStore _policies;
    private readonly JitOptions _jitOptions;
    private readonly ILogger<AccessRequestsController> _logger;

    public AccessRequestsController(
        IAccessRequestStore store,
        ITargetStore targets,
        IAuditStore audit,
        IApprovalStore approvals,
        IItsmClient itsmClient,
        AccessPolicyEvaluator policyEvaluator,
        IPolicyStore policies,
        IOptions<JitOptions> jitOptions,
        ILogger<AccessRequestsController> logger)
    {
        _store = store;
        _targets = targets;
        _audit = audit;
        _approvals = approvals;
        _itsmClient = itsmClient;
        _policyEvaluator = policyEvaluator;
        _policies = policies;
        _jitOptions = jitOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_store.GetAll());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AccessRequestCreateDto dto, CancellationToken cancellationToken)
    {
        var target = _targets.GetById(dto.TargetId);
        if (target is null)
        {
            return NotFound(new { message = "Target not found" });
        }

        if (!_policyEvaluator.IsRequestAllowed(User, target, out var denyReason))
        {
            return Forbid(denyReason);
        }

        var hasPolicies = _policies.GetAll().Any(p =>
            string.Equals(p.Effect, "Allow", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.TargetType, target.Type, StringComparison.OrdinalIgnoreCase));
        if (!hasPolicies)
        {
            return UnprocessableEntity(new { message = "No matching policy exists for the target type." });
        }

        var username = User.Identity?.Name ?? "unknown";
        var activeCount = _store.GetAll().Count(r =>
            r.RequestedBy == username &&
            (r.Status == AccessRequestStatus.Pending || r.Status == AccessRequestStatus.Approved));
        if (activeCount >= _jitOptions.MaxActiveRequestsPerUser)
        {
            return Conflict(new { message = $"Active request limit ({_jitOptions.MaxActiveRequestsPerUser}) reached." });
        }

        var now = DateTimeOffset.UtcNow;
        var request = new AccessRequest(
            $"REQ-{Guid.NewGuid():N}",
            dto.TargetId,
            username,
            dto.DurationMinutes,
            dto.Reason,
            AccessRequestStatus.Pending,
            now,
            now.AddMinutes(dto.DurationMinutes),
            null
        );

        try
        {
            var itsmTicket = await _itsmClient.CreateAccessRequestAsync(
                new ItsmAccessRequest(
                    $"PAM JIT: {target.Name}",
                    $"Запрос доступа к {target.Name} на {dto.DurationMinutes} минут. Причина: {dto.Reason}",
                    request.RequestedBy,
                    request.TargetId,
                    dto.DurationMinutes.ToString()),
                cancellationToken);

            request = request with { ItsmKey = itsmTicket.Key };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ITSM ticket creation failed — request will be created without Jira link.");
        }

        _store.Add(request);
        _audit.Add(AuditEventFactory.Create(HttpContext, "access.requested", "request", "pending", request.TargetId, target.Name, request.Id));
        if (!string.IsNullOrWhiteSpace(request.ItsmKey))
        {
            await _itsmClient.UpdateStatusAsync(request.ItsmKey, "pending", cancellationToken);
        }

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var request = _store.GetById(id);
        if (request is null)
        {
            return NotFound(new { message = "Request not found" });
        }

        return Ok(request);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "PAM_Administrator,Security_Auditor")]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken)
    {
        var request = _store.GetById(id);
        if (request is null)
        {
            return NotFound(new { message = "Request not found" });
        }

        if (request.Status != AccessRequestStatus.Pending)
        {
            return Conflict(new { message = "Request is not pending" });
        }

        var updated = request with { Status = AccessRequestStatus.Approved };
        _store.Update(updated);

        var target = _targets.GetById(request.TargetId);
        _audit.Add(AuditEventFactory.Create(HttpContext, "access.approved", "approve", "success", request.TargetId, target?.Name ?? "", request.Id));

        if (!string.IsNullOrWhiteSpace(request.ItsmKey))
        {
            try
            {
                await _itsmClient.UpdateStatusAsync(request.ItsmKey, "approved", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Jira ticket status.");
            }
        }

        _approvals.Add(new Approval(
            $"APR-{Guid.NewGuid():N}",
            request.Id,
            User.Identity?.Name ?? "unknown",
            DateTimeOffset.UtcNow,
            "approved"));

        return Ok(updated);
    }

    [HttpPost("{id}/deny")]
    [Authorize(Roles = "PAM_Administrator,Security_Auditor")]
    public async Task<IActionResult> Deny(string id, CancellationToken cancellationToken)
    {
        var request = _store.GetById(id);
        if (request is null)
        {
            return NotFound(new { message = "Request not found" });
        }

        if (request.Status != AccessRequestStatus.Pending)
        {
            return Conflict(new { message = "Request is not pending" });
        }

        var updated = request with { Status = AccessRequestStatus.Denied };
        _store.Update(updated);

        var target = _targets.GetById(request.TargetId);
        _audit.Add(AuditEventFactory.Create(HttpContext, "access.denied", "deny", "success", request.TargetId, target?.Name ?? "", request.Id));

        if (!string.IsNullOrWhiteSpace(request.ItsmKey))
        {
            try
            {
                await _itsmClient.UpdateStatusAsync(request.ItsmKey, "denied", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Jira ticket status.");
            }
        }

        _approvals.Add(new Approval(
            $"APR-{Guid.NewGuid():N}",
            request.Id,
            User.Identity?.Name ?? "unknown",
            DateTimeOffset.UtcNow,
            "denied"));

        return Ok(updated);
    }
}
