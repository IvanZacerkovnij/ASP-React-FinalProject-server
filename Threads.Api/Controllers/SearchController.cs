using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Gifs;
using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Gifs;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;
    private readonly IGifSearchService _gifSearchService;

    public SearchController(
        IUserService userService,
        IPostService postService,
        IGifSearchService gifSearchService)
    {
        _userService = userService;
        _postService = postService;
        _gifSearchService = gifSearchService;
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

    [HttpGet("gifs")]
    public async Task<ActionResult<IReadOnlyCollection<GifResponse>>> SearchGifs(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        try
        {
            var gifs = await _gifSearchService.SearchAsync(q ?? string.Empty, cancellationToken);
            return Ok(gifs);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }
}
