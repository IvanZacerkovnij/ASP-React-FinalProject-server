using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
using Threads.Api.Responses;
using Threads.Application.DTOs.Admin;
using Threads.Application.Interfaces.Admin;
using Threads.Infrastructure.Security;

namespace Threads.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[Route("api/admin/moderation")]
public sealed class AdminModerationController : ControllerBase
{
    private readonly IAdministrationService _administrationService;

    public AdminModerationController(IAdministrationService administrationService)
    {
        _administrationService = administrationService;
    }

    [HttpGet]
    [ProducesResponseType<AdminPageResponse<ReportResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPageResponse<ReportResponse>>> GetReports(
        [FromQuery] ModerationQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _administrationService.GetReportsAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReportResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportResponse>> GetReport(Guid id, CancellationToken cancellationToken)
    {
        var report = await _administrationService.GetReportAsync(id, cancellationToken);
        return report is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Report was not found.")
            : Ok(report);
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<ReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReportResponse>> Resolve(
        Guid id,
        [FromBody] ResolveReportRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _administrationService.ResolveReportAsync(
            id,
            User.GetRequiredCurrentUserId(),
            request,
            cancellationToken));
    }
}
