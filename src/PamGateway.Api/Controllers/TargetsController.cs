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
    public IActionResult GetAll() => Ok(_targets.GetAll());

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
}
