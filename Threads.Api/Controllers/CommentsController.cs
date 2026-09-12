using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
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
        [FromRoute] Guid postId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var comments = await _commentService.GetByPostIdAsync(
            postId,
            cancellationToken,
            currentUserId);
        return Ok(comments);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> Create(
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var comment = await _commentService.CreateAsync(currentUserId.Value, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = comment.Id }, comment);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CommentResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var comment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);
        if (comment is null)
        {
            return NotFound(new { message = "Comment was not found." });
        }
        return Ok(comment);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CommentResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var existingComment = await _commentService.GetByIdAsync(id, cancellationToken);

        if (existingComment is null)
        {
            return NotFound(new { message = "Comment was not found." });
        }

        if (existingComment.Author.Id != currentUserId.Value)
        {
            return Forbid();
        }

        var updatedComment = await _commentService.UpdateAsync(
            id,
            request,
            cancellationToken,
            currentUserId.Value);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var existingComment = await _commentService.GetByIdAsync(id, cancellationToken);

        if (existingComment is null)
        {
            return NotFound(new { message = "Comment was not found." });
        }

        if (existingComment.Author.Id != currentUserId.Value)
        {
            return Forbid();
        }

        var wasDeleted = await _commentService.DeleteAsync(id, cancellationToken);

        return wasDeleted
            ? NoContent()
            : NotFound(new { message = "Comment was not found." });
    }

    [Authorize]
    [HttpPost("{id:guid}/like")]
    public async Task<ActionResult<CommentResponse>> LikeComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.LikeAsync(id, currentUserId.Value, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpPost("{id:guid}/view")]
    public async Task<ActionResult<CommentResponse>> ViewComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.ViewAsync(id, currentUserId.Value, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpDelete("{id:guid}/like")]
    public async Task<ActionResult<CommentResponse>> UnlikeComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.UnlikeAsync(id, currentUserId.Value, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpPost("{id:guid}/bookmark")]
    public async Task<ActionResult<CommentResponse>> BookmarkComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.BookmarkAsync(id, currentUserId.Value, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpDelete("{id:guid}/bookmark")]
    public async Task<ActionResult<CommentResponse>> UnbookmarkComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.UnbookmarkAsync(id, currentUserId.Value, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpPost("{id:guid}/repost")]
    public async Task<ActionResult<CommentResponse>> RepostComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.RepostAsync(id, currentUserId.Value, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }

    [Authorize]
    [HttpDelete("{id:guid}/repost")]
    public async Task<ActionResult<CommentResponse>> UnrepostComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var updatedComment = await _commentService.UnrepostAsync(id, currentUserId.Value, cancellationToken);

        return updatedComment is null
            ? NotFound(new { message = "Comment was not found." })
            : Ok(updatedComment);
    }
}
