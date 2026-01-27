using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/policies")]
[Authorize(Roles = "PAM_Administrator")]
public sealed class PoliciesController : ControllerBase
{
    private readonly IPolicyStore _policies;

    public PoliciesController(IPolicyStore policies)
    {
        _policies = policies;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_policies.GetAll());

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var policy = _policies.GetById(id);
        if (policy is null)
        {
            return NotFound(new { message = "Policy not found" });
        }

        return Ok(policy);
    }

    [HttpPost]
    public IActionResult Create([FromBody] PolicyCreateDto dto)
    {
        var policy = new Policy($"POL-{Guid.NewGuid():N}", dto.Name, dto.TargetType, dto.AllowedProtocols, dto.Effect);
        _policies.Add(policy);
        return CreatedAtAction(nameof(GetById), new { id = policy.Id }, policy);
    }
}
