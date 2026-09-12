using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
using Threads.Api.Requests.Media;
using Threads.Application.DTOs.Media;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;

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
    public async Task<ActionResult<MediaUrlResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var media = await _mediaService.GetUrlAsync(id, currentUserId, cancellationToken);

        return media is null
            ? NotFound(new { message = "Media was not found." })
            : Ok(media);
    }

    [Authorize]
    [HttpPost("upload")]
    [RequestSizeLimit(104_857_600)]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    public async Task<ActionResult<UploadMediaResponse>> Upload(
        [FromForm] UploadMediaRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { message = "File is required." });
        }

        try
        {
            await using var stream = request.File.OpenReadStream();

            var media = await _mediaService.UploadAsync(
                currentUserId.Value,
                stream,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                cancellationToken);

            return Ok(media);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (MediaProcessingException exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
