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
    public IActionResult Get(
        [FromQuery] string? user,
        [FromQuery] string? target,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 100)
    {
        if (limit < 1) limit = 1;
        if (limit > 1000) limit = 1000;
        if (offset < 0) offset = 0;

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

        var filtered = events.ToList();
        var page = filtered.Skip(offset).Take(limit).ToList();

        return Ok(new
        {
            total = filtered.Count,
            offset,
            limit,
            items = page
        });
    }
}
