using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/approvals")]
[Authorize(Roles = "PAM_Administrator,Security_Auditor")]
public sealed class ApprovalsController : ControllerBase
{
    private readonly IApprovalStore _approvals;

    public ApprovalsController(IApprovalStore approvals)
    {
        _approvals = approvals;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_approvals.GetAll());

    [HttpPost]
    public IActionResult Create([FromBody] ApprovalCreateDto dto)
    {
        var approval = new Approval(
            $"APR-{Guid.NewGuid():N}",
            dto.RequestId,
            dto.Approver,
            DateTimeOffset.UtcNow,
            dto.Status);

        _approvals.Add(approval);
        return Ok(approval);
    }
}
