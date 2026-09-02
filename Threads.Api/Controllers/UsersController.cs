using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Requests;
using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;

    public UsersController(IUserService userService, IPostService postService)
    {
        _userService = userService;
        _postService = postService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserShortResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userService.GetAllAsync(cancellationToken, GetCurrentUserId());
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken, GetCurrentUserId());

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }
    [HttpGet("{username}")]
    public async Task<ActionResult<UserResponse>> GetByUsername(string username, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken, GetCurrentUserId());

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }

    [HttpGet("by-username/{username}")]
    public async Task<ActionResult<UserResponse>> GetByUsernameExplicit(string username, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken, GetCurrentUserId());

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }

    [HttpGet("{id:guid}/posts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetPostsByUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var currentUserId = GetCurrentUserId();
        var posts = await _postService.GetByAuthorIdAsync(id, cancellationToken, currentUserId);

        return Ok(posts);
    }

    [HttpGet("{username}/likes")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetLikedPostsByUsername(
        string username,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var currentUserId = GetCurrentUserId();
        var posts = await _postService.GetLikedByUserIdAsync(user.Id, cancellationToken, currentUserId);

        return Ok(posts);
    }

    [HttpGet("{username}/reposts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetRepostedPostsByUsername(
        string username,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var currentUserId = GetCurrentUserId();
        var posts = await _postService.GetRepostedByUserIdAsync(user.Id, cancellationToken, currentUserId);

        return Ok(posts);
    }

    [Authorize]
    [Consumes("multipart/form-data")]
    [HttpPut("me")]
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

    [Authorize]
    [HttpDelete("me")]
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

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }
}
