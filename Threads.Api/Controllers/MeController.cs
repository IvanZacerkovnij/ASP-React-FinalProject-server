using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Requests.Users;
using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;
    
    public MeController(
        IUserService userService,
        IPostService postService)
    {
        _userService = userService;
        _postService = postService;
    }
    
    [HttpGet]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _userService.GetByIdAsync(userId.Value, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }
        return Ok(user);
    }
    
    [Consumes("multipart/form-data")]
    [HttpPut]
    public async Task<ActionResult<UserResponse>> UpdateMe(
        [FromForm] UpdateCurrentUserRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        await using var avatarStream = request.Avatar?.OpenReadStream();
        await using var bannerStream = request.Banner?.OpenReadStream();

        var updateRequest = new UpdateUserRequest
        {
            DisplayName = request.DisplayName,
            Bio = request.Bio,
            DateOfBirth = request.DateOfBirth,
            RemoveDateOfBirth = request.RemoveDateOfBirth,
            RemoveLocation = request.RemoveLocation,
            Location = request.Location,
            RemoveAvatar = request.RemoveAvatar,
            RemoveBanner = request.RemoveBanner
        };

        var avatar = request.Avatar is null
            ? null
            : new UserFileUploadRequest
            {
                Content = avatarStream!,
                FileName = request.Avatar.FileName,
                ContentType = request.Avatar.ContentType,
                SizeInBytes = request.Avatar.Length
            };

        var banner = request.Banner is null
            ? null
            : new UserFileUploadRequest
            {
                Content = bannerStream!,
                FileName = request.Banner.FileName,
                ContentType = request.Banner.ContentType,
                SizeInBytes = request.Banner.Length
            };

        try
        {
            var user = await _userService.UpdateAsync(currentUserId.Value, updateRequest, avatar, banner, cancellationToken);

            return user is null
                ? NotFound(new { message = "User was not found." })
                : Ok(user);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
    
    [HttpDelete]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var wasDeleted = await _userService.DeleteAsync(currentUserId.Value, cancellationToken);

        return wasDeleted
            ? NoContent()
            : NotFound(new { message = "User was not found." });
    }
    
    [HttpGet("posts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetPosted(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }
        
        var posts = await _postService.GetByAuthorIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);
        return Ok(posts);
    }
    
    [HttpGet("likes")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetLiked(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetLikedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(posts);
    }
    
    [HttpGet("bookmarks")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetBookmarked(
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetBookmarkedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(posts);
    }
    
    [HttpGet("reposts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetReposted(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetRepostedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(posts);
    }

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }
}
