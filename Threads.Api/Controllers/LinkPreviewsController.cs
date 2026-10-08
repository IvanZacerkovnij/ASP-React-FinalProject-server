using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.Interfaces.LinkPreviews;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LinkPreviewsController : ControllerBase
{
    private readonly ILinkPreviewService _linkPreviewService;

    public LinkPreviewsController(ILinkPreviewService linkPreviewService)
    {
        _linkPreviewService = linkPreviewService;
    }

    [HttpPost("resolve")]
    [EnableRateLimiting(RateLimiterConfigurator.ExternalSearchPolicyName)]
    [ProducesResponseType<LinkPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<LinkPreviewResponse>> Resolve(
        [FromBody] ResolveLinkPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _linkPreviewService.ResolveAsync(request.Url, cancellationToken);
        return Ok(response);
    }
}
