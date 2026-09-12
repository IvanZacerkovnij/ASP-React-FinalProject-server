using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
using Threads.Application.DTOs.Gifs;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Gifs;
using Threads.Application.Interfaces.Locations;
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
    private readonly ILocationSearchService _locationSearchService;

    public SearchController(
        IUserService userService,
        IPostService postService,
        IGifSearchService gifSearchService,
        ILocationSearchService locationSearchService)
    {
        _userService = userService;
        _postService = postService;
        _gifSearchService = gifSearchService;
        _locationSearchService = locationSearchService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyCollection<UserShortResponse>>> SearchUsers(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var users = await _userService.SearchAsync(q ?? string.Empty, cancellationToken, currentUserId);
        return Ok(users);
    }

    [HttpGet("posts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> SearchPosts(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var posts = await _postService.SearchAsync(q ?? string.Empty, cancellationToken, currentUserId);
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

    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyCollection<LocationResponse>>> SearchLocations(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        try
        {
            var locations = await _locationSearchService.SearchAsync(q ?? string.Empty, cancellationToken);
            return Ok(locations);
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
}
