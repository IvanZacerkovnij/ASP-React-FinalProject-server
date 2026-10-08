using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Api.Responses;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Users;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/follows")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public class FollowsController : ControllerBase
{
    private readonly IFollowService _followService;
    private readonly IUserService _userService;

    public FollowsController(IFollowService followService, IUserService userService)
    {
        _followService = followService;
        _userService = userService;
    }

    [Authorize]
    [HttpPost("{userId:guid}")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Follow(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound);
        }

        var wasAdded = await _followService.AddFollowAsync(currentUserId, userId, cancellationToken);

        return wasAdded
            ? Ok(new { message = "User followed successfully." })
            : this.ProblemResponse(StatusCodes.Status409Conflict, "Unable to follow this user.");
    }

    [Authorize]
    [HttpDelete("{userId:guid}")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Unfollow(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound);
        }

        var wasRemoved = await _followService.RemoveFollowAsync(currentUserId, userId, cancellationToken);

        return wasRemoved
            ? NoContent()
            : this.ProblemResponse(StatusCodes.Status404NotFound, "Follow was not found.");
    }

    [HttpGet("{userId:guid}/followers")]
    [ProducesResponseType<CursorPageResponse<UserShortResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<UserShortResponse>>> GetFollowers(
        [FromRoute] Guid userId,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound);
        }

        var followers = await _followService.GetFollowersAsync(userId, pagination, cancellationToken);

        return Ok(followers);
    }

    [HttpGet("{userId:guid}/following")]
    [ProducesResponseType<CursorPageResponse<UserShortResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<UserShortResponse>>> GetFollowing(
        [FromRoute] Guid userId,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound);
        }

        var following = await _followService.GetFollowingAsync(userId, pagination, cancellationToken);

        return Ok(following);
    }

    [Authorize]
    [HttpDelete("{userId:guid}/followers/{followId:guid}")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> RemoveFollower(
        [FromRoute] Guid userId,
        [FromRoute] Guid followId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        await _followService.RemoveFollowerAsync(
            currentUserId,
            userId,
            followId,
            cancellationToken);

        return NoContent();
    }
}
