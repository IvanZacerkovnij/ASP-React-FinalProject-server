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
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdministrationService _administrationService;

    public AdminUsersController(IAdministrationService administrationService)
    {
        _administrationService = administrationService;
    }

    [HttpGet]
    [ProducesResponseType<AdminPageResponse<AdminUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminPageResponse<AdminUserResponse>>> GetUsers(
        [FromQuery] AdminUsersQuery query,
        CancellationToken cancellationToken) =>
        Ok(await _administrationService.GetUsersAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminUserResponse>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _administrationService.GetUserAsync(id, cancellationToken);
        return user is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound)
            : Ok(user);
    }

    [HttpPut("{id:guid}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Block(Guid id, CancellationToken cancellationToken) =>
        SetBlockedAsync(id, true, cancellationToken);

    [HttpPut("{id:guid}/unblock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Unblock(Guid id, CancellationToken cancellationToken) =>
        SetBlockedAsync(id, false, cancellationToken);

    [HttpDelete("{id:guid}/delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _administrationService.DeleteUserAsync(
            id,
            User.GetRequiredCurrentUserId(),
            cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> SetBlockedAsync(Guid id, bool blocked, CancellationToken cancellationToken)
    {
        await _administrationService.SetUserBlockedAsync(
            id,
            User.GetRequiredCurrentUserId(),
            blocked,
            cancellationToken);
        return NoContent();
    }
}
