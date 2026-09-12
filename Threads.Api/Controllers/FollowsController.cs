using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Users;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/follows")]
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
    public async Task<ActionResult> Follow(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var wasAdded = await _followService.AddFollowAsync(currentUserId.Value, userId, cancellationToken);

        return wasAdded
            ? Ok(new { message = "User followed successfully." })
            : Conflict(new { message = "Unable to follow this user." });
    }

    [Authorize]
    [HttpDelete("{userId:guid}")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult> Unfollow(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var wasRemoved = await _followService.RemoveFollowAsync(currentUserId.Value, userId, cancellationToken);

        return wasRemoved
            ? NoContent()
            : NotFound(new { message = "Follow was not found." });
    }

    [HttpGet("{userId:guid}/followers")]
    public async Task<ActionResult<IReadOnlyCollection<UserShortResponse>>> GetFollowers(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var followers = await _followService.GetFollowersAsync(userId, cancellationToken);

        return Ok(followers);
    }

    [HttpGet("{userId:guid}/following")]
    public async Task<ActionResult<IReadOnlyCollection<UserShortResponse>>> GetFollowing(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var following = await _followService.GetFollowingAsync(userId, cancellationToken);

        return Ok(following);
    }

    [Authorize]
    [HttpDelete("{userId:guid}/followers/{followId:guid}")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult> RemoveFollower(
        [FromRoute] Guid userId,
        [FromRoute] Guid followId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        if (currentUserId.Value != userId)
        {
            return Forbid();
        }

        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var follower = await _userService.GetByIdAsync(followId, cancellationToken);

        if (follower is null)
        {
            return NotFound(new { message = "Follower was not found." });
        }

        var wasRemoved = await _followService.RemoveFollowAsync(followId, userId, cancellationToken);

        return wasRemoved
            ? NoContent()
            : NotFound(new { message = "Follow was not found." });
    }
}
