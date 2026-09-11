using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Likes;
using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Reposts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;
    private readonly ICommentService _commentService;

    public UsersController(
        IUserService userService,
        IPostService postService,
        ICommentService commentService)
    {
        _userService = userService;
        _postService = postService;
        _commentService = commentService;
    }

    [HttpGet("by-id/{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken, GetCurrentUserId());

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }
    [HttpGet("by-username/{username}")]
    public async Task<ActionResult<UserResponse>> GetByUsername(
        [FromRoute] string username,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken, GetCurrentUserId());

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }

    [HttpGet("{username}/posts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetPostsByUsername(
        [FromRoute] string username,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var posts = await _postService.GetByAuthorIdAsync(
            user.Id,
            cancellationToken,
            GetCurrentUserId());
        return Ok(posts);
    }

    [HttpGet("{username}/likes")]
    public async Task<ActionResult<UserLikesResponse>> GetLikedByUsername(
        [FromRoute] string username,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var currentUserId = GetCurrentUserId();
        var posts = await _postService.GetLikedByUserIdAsync(user.Id, cancellationToken, currentUserId);
        var comments = await _commentService.GetLikedByUserIdAsync(user.Id, cancellationToken, currentUserId);

        return Ok(new UserLikesResponse
        {
            Posts = posts,
            Comments = comments
        });
    }

    [HttpGet("{username}/reposts")]
    public async Task<ActionResult<UserRepostsResponse>> GetRepostedByUsername(
        [FromRoute] string username,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var currentUserId = GetCurrentUserId();
        var posts = await _postService.GetRepostedByUserIdAsync(user.Id, cancellationToken, currentUserId);
        var comments = await _commentService.GetRepostedByUserIdAsync(user.Id, cancellationToken, currentUserId);

        return Ok(new UserRepostsResponse
        {
            Posts = posts,
            Comments = comments
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }
}
