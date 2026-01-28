using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/targets")]
[Authorize]
public sealed class TargetsController : ControllerBase
{
    private readonly ITargetStore _targets;

    public TargetsController(ITargetStore targets)
    {
        _targets = targets;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var targets = _targets.GetAll();
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(targets);
        }

        if (User.IsInRole("PAM_Administrator") || User.IsInRole("Security_Auditor"))
        {
            return Ok(targets);
        }

        var required = new List<(string Key, string Value)>();
        if (User.IsInRole("System_Admin_Windows"))
        {
            required.Add(("os", "windows"));
        }
        if (User.IsInRole("System_Admin_Linux"))
        {
            required.Add(("os", "linux"));
        }

        if (required.Count == 0)
        {
            return Ok(Array.Empty<TargetSystem>());
        }

        var filtered = targets.Where(target => MatchesLabels(target, required)).ToList();
        return Ok(filtered);
    }

    [HttpPost]
    [Authorize(Roles = "PAM_Administrator")]
    public IActionResult Create([FromBody] TargetUpsertDto dto)
    {
        var target = Map(dto);
        _targets.AddOrUpdate(target);
        return CreatedAtAction(nameof(GetById), new { id = target.Id }, target);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var target = _targets.GetById(id);
        if (target is null)
        {
            return NotFound(new { message = "Target not found" });
        }

        return Ok(target);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "PAM_Administrator")]
    public IActionResult Update(string id, [FromBody] TargetUpsertDto dto)
    {
        if (!string.Equals(id, dto.Id, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Id mismatch" });
        }

        var target = Map(dto);
        _targets.AddOrUpdate(target);
        return Ok(target);
    }

    private static bool MatchesLabels(TargetSystem target, IReadOnlyList<(string Key, string Value)> required)
    {
        if (target.Labels is null || target.Labels.Count == 0)
        {
            return false;
        }

        foreach (var (key, value) in required)
        {
            if (target.Labels.TryGetValue(key, out var labelValue)
                && string.Equals(labelValue, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static TargetSystem Map(TargetUpsertDto dto)
        => new(
            dto.Id,
            dto.Name,
            dto.Host,
            dto.Port,
            dto.Labels,
            dto.Type,
            dto.Environment,
            dto.Criticality,
            dto.Status
        );
}
