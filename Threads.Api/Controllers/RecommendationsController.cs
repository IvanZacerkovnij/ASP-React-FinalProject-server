using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Recommendations;
using Threads.Api.Extensions;

namespace Threads.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class RecommendationsController(IRecommendationService service) : ControllerBase
{
    [HttpGet("posts")]
    [ProducesResponseType<CursorPageResponse<PostResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<PostResponse>>> GetPosts(
        [FromQuery] CursorPageRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.GetPostsAsync(User.GetRequiredCurrentUserId(), request, cancellationToken));
    }

    [HttpGet("users")]
    [ProducesResponseType<CursorPageResponse<UserShortResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<UserShortResponse>>> GetUsers(
        [FromQuery] CursorPageRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.GetUsersAsync(User.GetRequiredCurrentUserId(), request, cancellationToken));
    }
}
