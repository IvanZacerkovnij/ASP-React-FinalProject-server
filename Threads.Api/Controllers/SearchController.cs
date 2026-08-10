using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;

    public SearchController(IUserService userService, IPostService postService)
    {
        _userService = userService;
        _postService = postService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyCollection<UserShortResponse>>> SearchUsers(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var users = await _userService.SearchAsync(q ?? string.Empty, cancellationToken, GetCurrentUserId());
        return Ok(users);
    }

    [HttpGet("posts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> SearchPosts(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var posts = await _postService.SearchAsync(q ?? string.Empty, cancellationToken, GetCurrentUserId());
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
