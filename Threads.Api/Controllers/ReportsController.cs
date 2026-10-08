using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
using Threads.Api.Responses;
using Threads.Application.DTOs.Admin;
using Threads.Application.Interfaces.Admin;
using Threads.Domain.Enums;

namespace Threads.Api.Controllers;

[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ReportsController : ControllerBase
{
    private readonly IAdministrationService _administrationService;

    public ReportsController(IAdministrationService administrationService)
    {
        _administrationService = administrationService;
    }

    [HttpPost("api/posts/{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReportPost(
        Guid id,
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken) =>
        CreateAsync(ReportTargetType.Posts, id, request, cancellationToken);

    [HttpPost("api/comments/{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReportComment(
        Guid id,
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken) =>
        CreateAsync(ReportTargetType.Comments, id, request, cancellationToken);

    [HttpPost("api/users/{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ReportUser(
        Guid id,
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken) =>
        CreateAsync(ReportTargetType.Users, id, request, cancellationToken);

    private async Task<IActionResult> CreateAsync(
        ReportTargetType targetType,
        Guid targetId,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        await _administrationService.CreateReportAsync(
            targetType,
            targetId,
            User.GetRequiredCurrentUserId(),
            request,
            cancellationToken);
        return NoContent();
    }
}
