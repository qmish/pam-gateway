using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/cmdb")]
[Authorize(Roles = "PAM_Administrator")]
public sealed class CmdbController : ControllerBase
{
    private readonly ICmdbClient _cmdb;
    private readonly ITargetStore _targets;

    public CmdbController(ICmdbClient cmdb, ITargetStore targets)
    {
        _cmdb = cmdb;
        _targets = targets;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
    {
        var cmdbTargets = await _cmdb.FetchTargetsAsync(cancellationToken);
        var targets = cmdbTargets
            .Select(item => new TargetSystem(
                item.Id,
                item.Name,
                null,
                null,
                item.Type,
                item.Environment,
                item.Criticality,
                item.Status))
            .ToList();

        _targets.AddOrUpdateRange(targets);

        return Ok(new { imported = targets.Count });
    }
}
