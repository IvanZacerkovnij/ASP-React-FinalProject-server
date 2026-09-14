using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Likes;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Reposts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Interfaces.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;
    private readonly ILikeService _likeService;
    private readonly IRepostService _repostService;

    public UsersController(
        IUserService userService,
        IPostService postService,
        ILikeService likeService,
        IRepostService repostService)
    {
        _userService = userService;
        _postService = postService;
        _likeService = likeService;
        _repostService = repostService;
    }

    [HttpGet("by-id/{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var user = await _userService.GetByIdAsync(id, cancellationToken, currentUserId);

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }
    [HttpGet("by-username/{username}")]
    public async Task<ActionResult<UserResponse>> GetByUsername(
        [FromRoute] string username,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var user = await _userService.GetByUsernameAsync(username, cancellationToken, currentUserId);

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(user);
    }

    [HttpGet("{username}/posts")]
    public async Task<ActionResult<CursorPageResponse<PostResponse>>> GetPostsByUsername(
        [FromRoute] string username,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);
        
        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var posts = await _postService.GetByAuthorIdAsync(
            user.Id,
            pagination,
            cancellationToken,
            currentUserId);
        return Ok(posts);
    }

    [HttpGet("{username}/likes")]
    public async Task<ActionResult<UserLikesPageResponse>> GetLikedByUsername(
        [FromRoute] string username,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var currentUserId = User.GetCurrentUserId();
        var likes = await _likeService.GetByUserIdAsync(
            user.Id,
            pagination,
            cancellationToken,
            currentUserId);

        return Ok(likes);
    }

    [HttpGet("{username}/reposts")]
    public async Task<ActionResult<UserRepostsPageResponse>> GetRepostedByUsername(
        [FromRoute] string username,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var currentUserId = User.GetCurrentUserId();
        var reposts = await _repostService.GetByUserIdAsync(
            user.Id,
            pagination,
            cancellationToken,
            currentUserId);

        return Ok(reposts);
    }
}
