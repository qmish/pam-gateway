using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IServiceProvider _services;

    public HealthController(IServiceProvider services)
    {
        _services = services;
    }

    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });

    [HttpGet("live")]
    public IActionResult Liveness() => Ok(new { status = "alive" });

    [HttpGet("ready")]
    public IActionResult Readiness()
    {
        var checks = new List<object>();
        var allHealthy = true;

        allHealthy &= CheckStore<ITargetStore>("targets", checks);
        allHealthy &= CheckStore<IAccessRequestStore>("requests", checks);
        allHealthy &= CheckStore<ISessionStore>("sessions", checks);
        allHealthy &= CheckStore<IAgentStore>("agents", checks);

        if (!allHealthy)
            return StatusCode(503, new { status = "not_ready", checks });

        return Ok(new { status = "ready", checks });
    }

    private bool CheckStore<T>(string name, List<object> checks)
    {
        try
        {
            using var scope = _services.CreateScope();
            var store = scope.ServiceProvider.GetService<T>();
            if (store is null)
            {
                checks.Add(new { name, status = "missing" });
                return false;
            }
            checks.Add(new { name, status = "ok" });
            return true;
        }
        catch (Exception ex)
        {
            checks.Add(new { name, status = "error", message = ex.Message });
            return false;
        }
    }
}
