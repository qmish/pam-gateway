using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/integrations/jira")]
public sealed class JiraWebhooksController : ControllerBase
{
    private readonly IAccessRequestStore _requests;
    private readonly ITargetStore _targets;
    private readonly IApprovalStore _approvals;
    private readonly IAuditStore _audit;
    private readonly JiraOptions _options;
    private readonly ILogger<JiraWebhooksController> _logger;

    public JiraWebhooksController(
        IAccessRequestStore requests,
        ITargetStore targets,
        IApprovalStore approvals,
        IAuditStore audit,
        IOptions<JiraOptions> options,
        ILogger<JiraWebhooksController> logger)
    {
        _requests = requests;
        _targets = targets;
        _approvals = approvals;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public IActionResult HandleWebhook([FromBody] JiraIssueWebhookDto payload)
    {
        if (!IsWebhookAuthorized())
        {
            return Unauthorized(new { message = "Invalid webhook token" });
        }

        var issueKey = payload.Issue?.Key;
        var statusName = payload.Issue?.Fields?.Status?.Name;
        if (string.IsNullOrWhiteSpace(issueKey) || string.IsNullOrWhiteSpace(statusName))
        {
            return BadRequest(new { message = "Missing issue key or status" });
        }

        var request = _requests.GetByItsmKey(issueKey);
        if (request is null)
        {
            return NotFound(new { message = "Access request not found for Jira issue" });
        }

        if (!TryMapStatus(statusName, out var mappedStatus))
        {
            _logger.LogInformation("Jira status '{Status}' has no mapping. Request {RequestId} left unchanged.", statusName, request.Id);
            return Ok(new { requestId = request.Id, status = request.Status.ToString().ToLowerInvariant(), ignored = true });
        }

        if (request.Status == mappedStatus)
        {
            return Ok(new { requestId = request.Id, status = request.Status.ToString().ToLowerInvariant(), ignored = true });
        }

        var updated = request with { Status = mappedStatus };
        _requests.Update(updated);

        if (mappedStatus is AccessRequestStatus.Approved or AccessRequestStatus.Denied)
        {
            _approvals.Add(new Approval(
                $"APR-{Guid.NewGuid():N}",
                request.Id,
                "jira",
                DateTimeOffset.UtcNow,
                mappedStatus == AccessRequestStatus.Approved ? "approved" : "denied"));
        }

        var target = _targets.GetById(request.TargetId);
        _audit.Add(new AuditEvent(
            DateTimeOffset.UtcNow,
            "access.status.sync",
            "jira",
            "jira",
            "system",
            request.TargetId,
            target?.Name ?? string.Empty,
            "status_update",
            mappedStatus.ToString().ToLowerInvariant(),
            request.Id,
            string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"));

        return Ok(new { requestId = updated.Id, status = mappedStatus.ToString().ToLowerInvariant() });
    }

    private bool IsWebhookAuthorized()
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return true;
        }

        var token = Request.Headers["X-Pam-Webhook-Token"].FirstOrDefault()
            ?? Request.Headers["X-Jira-Webhook-Token"].FirstOrDefault();

        return string.Equals(token, _options.WebhookSecret, StringComparison.Ordinal);
    }

    private bool TryMapStatus(string jiraStatus, out AccessRequestStatus status)
    {
        status = AccessRequestStatus.Pending;
        var mapped = MapFromConfig(jiraStatus);
        if (string.IsNullOrWhiteSpace(mapped))
        {
            mapped = jiraStatus;
        }

        if (Enum.TryParse(mapped, true, out status))
        {
            return true;
        }

        if (string.Equals(mapped, "rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapped, "declined", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapped, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapped, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            status = AccessRequestStatus.Denied;
            return true;
        }

        if (string.Equals(mapped, "reopened", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapped, "open", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mapped, "in progress", StringComparison.OrdinalIgnoreCase))
        {
            status = AccessRequestStatus.Pending;
            return true;
        }

        return false;
    }

    private string? MapFromConfig(string jiraStatus)
    {
        if (_options.StatusMap is null || _options.StatusMap.Count == 0)
        {
            return null;
        }

        foreach (var entry in _options.StatusMap)
        {
            if (string.Equals(entry.Key, jiraStatus, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }
}
