using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Api.Responses;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Versions;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Reposts;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _commentService;
    private readonly ILikeService _likeService;
    private readonly IRepostService _repostService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IPollService _pollService;

    public CommentsController(
        ICommentService commentService,
        ILikeService likeService,
        IRepostService repostService,
        IBookmarkService bookmarkService,
        IPollService pollService)
    {
        _commentService = commentService;
        _likeService = likeService;
        _repostService = repostService;
        _bookmarkService = bookmarkService;
        _pollService = pollService;
    }

    [HttpGet("post/{postId:guid}")]
    [ProducesResponseType<CursorPageResponse<CommentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<CommentResponse>>> GetByPostId(
        [FromRoute] Guid postId,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var comments = await _commentService.GetByPostIdAsync(
            postId,
            pagination,
            cancellationToken,
            currentUserId);
        return Ok(comments);
    }

    [Authorize]
    [HttpPost]
    [EnableRateLimiting(RateLimiterConfigurator.CommentCreationPolicyName)]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CommentResponse>> Create(
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var comment = await _commentService.CreateAsync(currentUserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = comment.Id }, comment);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status200OK)]
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
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound);
        }
        return Ok(comment);
    }

    [HttpGet("{id:guid}/thread")]
    [ProducesResponseType<CommentThreadResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentThreadResponse>> GetThread(
        [FromRoute] Guid id,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        var thread = await _commentService.GetThreadAsync(
            id,
            pagination,
            cancellationToken,
            currentUserId);

        return thread is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(thread);
    }

    [HttpGet("{id:guid}/edit-history")]
    [EnableRateLimiting(RateLimiterConfigurator.EditHistoryPolicyName)]
    [ProducesResponseType<EditHistoryResponse<CommentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EditHistoryResponse<CommentResponse>>> GetEditHistory(
        [FromRoute] Guid id,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        var history = await _commentService.GetEditHistoryAsync(
            id,
            pagination,
            cancellationToken,
            currentUserId);

        return history is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(history);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var updatedComment = await _commentService.UpdateAsync(
            id,
            currentUserId,
            request,
            cancellationToken);

        return Ok(updatedComment);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        await _commentService.DeleteAsync(id, currentUserId, cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/poll/vote")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<PollResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PollResponse>> VotePoll(
        [FromRoute] Guid id,
        [FromBody] VotePollRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var result = await _pollService.VoteCommentAsync(
            currentUserId,
            id,
            request,
            cancellationToken);

        return result.Status switch
        {
            PollVoteStatus.Success => Ok(result.Poll),
            PollVoteStatus.CommentNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound),
            PollVoteStatus.PollNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.PollNotFound),
            PollVoteStatus.InvalidOption => this.ProblemResponse(StatusCodes.Status400BadRequest, "Poll option is invalid."),
            PollVoteStatus.AlreadyVoted => this.ProblemResponse(StatusCodes.Status409Conflict, "You have already voted in this poll."),
            PollVoteStatus.PollClosed => this.ProblemResponse(StatusCodes.Status409Conflict, "Poll is already closed."),
            _ => this.ProblemResponse(StatusCodes.Status400BadRequest, "Unable to vote in poll.")
        };
    }

    [Authorize]
    [HttpPost("{id:guid}/like")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<CommentLikeStateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentLikeStateResponse>> LikeComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound);
        }

        await _likeService.AddCommentLikeAsync(currentUserId, id, cancellationToken);
        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(MapLikeStateResponse(updatedComment));
    }

    [Authorize]
    [HttpPost("{id:guid}/view")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<CommentViewResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentViewResponse>> RegisterView(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var result = await _commentService.RecordViewAsync(id, currentUserId, cancellationToken);

        return result is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(result);
    }

    [Authorize]
    [HttpDelete("{id:guid}/like")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<CommentLikeStateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentLikeStateResponse>> UnlikeComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound);
        }

        await _likeService.RemoveCommentLikeAsync(currentUserId, id, cancellationToken);
        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(MapLikeStateResponse(updatedComment));
    }

    [Authorize]
    [HttpPost("{id:guid}/bookmark")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<CommentBookmarkStateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentBookmarkStateResponse>> BookmarkComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound);
        }

        var wasAdded = await _bookmarkService.AddCommentBookmarkAsync(
            currentUserId,
            id,
            cancellationToken);

        if (!wasAdded)
        {
            var commentAfterFailedBookmark = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId);

            return commentAfterFailedBookmark is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
                : this.ProblemResponse(StatusCodes.Status409Conflict, "You have already bookmarked this comment.");
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(MapBookmarkStateResponse(updatedComment));
    }

    [Authorize]
    [HttpDelete("{id:guid}/bookmark")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<CommentBookmarkStateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentBookmarkStateResponse>> UnbookmarkComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound);
        }

        var wasRemoved = await _bookmarkService.RemoveCommentBookmarkAsync(
            currentUserId,
            id,
            cancellationToken);

        if (!wasRemoved)
        {
            var commentAfterFailedUnbookmark = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId);

            return commentAfterFailedUnbookmark is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
                : this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.BookmarkNotFound);
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(MapBookmarkStateResponse(updatedComment));
    }

    [Authorize]
    [HttpPost("{id:guid}/repost")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<CommentRepostStateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentRepostStateResponse>> RepostComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound);
        }

        var wasAdded = await _repostService.AddCommentRepostAsync(
            currentUserId,
            id,
            cancellationToken);

        if (!wasAdded)
        {
            var commentAfterFailedRepost = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId);

            return commentAfterFailedRepost is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
                : this.ProblemResponse(StatusCodes.Status409Conflict, "You have already reposted this comment.");
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(MapRepostStateResponse(updatedComment));
    }

    [Authorize]
    [HttpDelete("{id:guid}/repost")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    [ProducesResponseType<CommentRepostStateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentRepostStateResponse>> UnrepostComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound);
        }

        var wasRemoved = await _repostService.RemoveCommentRepostAsync(
            currentUserId,
            id,
            cancellationToken);

        if (!wasRemoved)
        {
            var commentAfterFailedUndo = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId);

            return commentAfterFailedUndo is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
                : this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.RepostNotFound);
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.CommentNotFound)
            : Ok(MapRepostStateResponse(updatedComment));
    }

    private static CommentLikeStateResponse MapLikeStateResponse(CommentResponse comment)
    {
        return new CommentLikeStateResponse
        {
            LikedByMe = comment.IsLikedByCurrentUser,
            LikesCount = comment.LikesCount
        };
    }

    private static CommentRepostStateResponse MapRepostStateResponse(CommentResponse comment)
    {
        return new CommentRepostStateResponse
        {
            RepostedByMe = comment.IsRepostedByCurrentUser,
            RepostsCount = comment.RepostsCount
        };
    }

    private static CommentBookmarkStateResponse MapBookmarkStateResponse(CommentResponse comment)
    {
        return new CommentBookmarkStateResponse
        {
            BookmarkedByMe = comment.IsBookmarkedByCurrentUser
        };
    }
}
