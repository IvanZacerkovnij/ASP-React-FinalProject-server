using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Comments;
using Threads.Application.Interfaces.Comments;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentsController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet("post/{postId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<CommentResponse>>> GetByPostId(
        Guid postId,
        CancellationToken cancellationToken)
    {
        var comments = await _commentService.GetByPostIdAsync(postId, cancellationToken);
        return Ok(comments);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> Create(
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        try
        {
            var comment = await _commentService.CreateAsync(currentUserId.Value, request, cancellationToken);
            return CreatedAtAction(nameof(GetByPostId), new { postId = comment.PostId }, comment);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CommentResponse>> Update(
        Guid id,
        UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.UpdateAsync(id, request, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var wasDeleted = await _commentService.DeleteAsync(id, cancellationToken);

        return wasDeleted
            ? NoContent()
            : NotFound(new { message = "Comment was not found." });
    }

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }
}
