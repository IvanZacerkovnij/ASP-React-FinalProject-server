using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
using Threads.Application.DTOs.Notifications;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Notifications;

namespace Threads.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CursorPageResponse<NotificationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CursorPageResponse<NotificationResponse>>> Get(
        [FromQuery] CursorPageRequest pagination, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAsync(User.GetRequiredCurrentUserId(), pagination, cancellationToken));
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadNotificationCountResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadNotificationCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        return Ok(await service.GetUnreadCountAsync(User.GetRequiredCurrentUserId(), cancellationToken));
    }

    [HttpPatch("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await service.MarkAsReadAsync(User.GetRequiredCurrentUserId(), notificationId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await service.MarkAllAsReadAsync(User.GetRequiredCurrentUserId(), cancellationToken);
        return NoContent();
    }
}
