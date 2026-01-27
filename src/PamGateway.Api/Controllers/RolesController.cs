using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Authorize(Roles = "PAM_Administrator")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleStore _roles;

    public RolesController(IRoleStore roles)
    {
        _roles = roles;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_roles.GetAll());

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var role = _roles.GetById(id);
        if (role is null)
        {
            return NotFound(new { message = "Role not found" });
        }

        return Ok(role);
    }

    [HttpPost]
    public IActionResult Create([FromBody] RoleCreateDto dto)
    {
        var role = new Role($"ROLE-{Guid.NewGuid():N}", dto.Name, dto.Description);
        _roles.Add(role);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }
}
