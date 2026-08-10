using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Requests;
using Threads.Application.DTOs.Media;
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
        Guid id,
        CancellationToken cancellationToken)
    {
        var media = await _mediaService.GetUrlAsync(id, GetCurrentUserId(), cancellationToken);

        return media is null
            ? NotFound(new { message = "Media was not found." })
            : Ok(media);
    }

    [Authorize]
    [HttpPost("upload")]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    public async Task<ActionResult<UploadMediaResponse>> Upload(
        [FromForm] UploadMediaRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
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
