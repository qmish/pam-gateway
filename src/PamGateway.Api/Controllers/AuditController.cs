using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/audit/events")]
[Authorize(Roles = "Security_Auditor,PAM_Administrator")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditStore _audit;

    public AuditController(IAuditStore audit)
    {
        _audit = audit;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? user, [FromQuery] string? target, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        var events = _audit.GetAll().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(user))
        {
            events = events.Where(item => item.Username.Contains(user, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(target))
        {
            events = events.Where(item => item.TargetId.Contains(target, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            events = events.Where(item => item.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            events = events.Where(item => item.Timestamp <= to.Value);
        }

        return Ok(events.ToList());
    }
}
