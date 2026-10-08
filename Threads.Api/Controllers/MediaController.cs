using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Api.Responses;
using Threads.Api.Requests.Media;
using Threads.Application.DTOs.Media;
using Threads.Application.Interfaces.Media;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public MediaController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MediaAttachmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MediaAttachmentResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var media = await _mediaService.GetByIdAsync(id, currentUserId, cancellationToken);

        return media is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.MediaNotFound)
            : Ok(media);
    }

    [Authorize]
    [HttpPost("upload")]
    [EnableRateLimiting(RateLimiterConfigurator.MediaUploadPolicyName)]
    [RequestSizeLimit(104_857_600)]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    [ProducesResponseType<MediaAttachmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MediaAttachmentResponse>> Upload(
        [FromForm] UploadMediaRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        if (request.File is null || request.File.Length == 0)
        {
            return this.ProblemResponse(StatusCodes.Status400BadRequest, "File is required.");
        }

        await using var stream = request.File.OpenReadStream();

        var media = await _mediaService.UploadAsync(
            currentUserId,
            stream,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            cancellationToken);

        return Ok(media);
    }
}
