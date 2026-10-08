using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IAdministrationService _administrationService;

    public AdminDashboardController(IAdministrationService administrationService)
    {
        _administrationService = administrationService;
    }

    [HttpGet("metrics")]
    [ProducesResponseType<IReadOnlyCollection<DashboardMetricResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<DashboardMetricResponse>>> GetMetrics(
        CancellationToken cancellationToken) =>
        Ok(await _administrationService.GetMetricsAsync(cancellationToken));

    [HttpGet("charts")]
    [ProducesResponseType<DashboardChartsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardChartsResponse>> GetCharts(
        [FromQuery] string period = DashboardPeriodContract.SevenDays,
        CancellationToken cancellationToken = default) =>
        Ok(await _administrationService.GetChartsAsync(period, cancellationToken));

    [HttpGet("tables")]
    [ProducesResponseType<DashboardTablesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardTablesResponse>> GetTables(
        [FromQuery] int limit = AdminPaginationContract.DefaultTableLimit,
        CancellationToken cancellationToken = default) =>
        Ok(await _administrationService.GetTablesAsync(limit, cancellationToken));
}
