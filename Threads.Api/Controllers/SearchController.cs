using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Application.DTOs.Gifs;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Search;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Gifs;
using Threads.Application.Interfaces.Locations;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
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
    [ProducesResponseType<CursorPageResponse<UserShortResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<UserShortResponse>>> SearchUsers(
        [FromQuery] SearchUsersRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var users = await _userService.SearchAsync(
            request,
            cancellationToken,
            currentUserId);
        return Ok(users);
    }

    [HttpGet("posts")]
    [ProducesResponseType<CursorPageResponse<PostResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<PostResponse>>> SearchPosts(
        [FromQuery] SearchPostsRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var posts = await _postService.SearchAsync(
            request,
            cancellationToken,
            currentUserId);
        return Ok(posts);
    }

    [HttpGet("gifs")]
    [EnableRateLimiting(RateLimiterConfigurator.ExternalSearchPolicyName)]
    [ProducesResponseType<IReadOnlyCollection<GifResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<GifResponse>>> SearchGifs(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var gifs = await _gifSearchService.SearchAsync(q ?? string.Empty, cancellationToken);
        return Ok(gifs);
    }

    [HttpGet("locations")]
    [EnableRateLimiting(RateLimiterConfigurator.ExternalSearchPolicyName)]
    [ProducesResponseType<IReadOnlyCollection<LocationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<LocationResponse>>> SearchLocations(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var locations = await _locationSearchService.SearchAsync(q ?? string.Empty, cancellationToken);
        return Ok(locations);
    }
}
