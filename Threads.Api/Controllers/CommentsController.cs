using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
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
    public async Task<ActionResult<CommentResponse>> Create(
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
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
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.");
        }
        return Ok(comment);
    }

    [HttpGet("{id:guid}/thread")]
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
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(thread);
    }

    [HttpGet("{id:guid}/edit-history")]
    [EnableRateLimiting(RateLimiterConfigurator.EditHistoryPolicyName)]
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
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(history);
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
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var updatedComment = await _commentService.UpdateAsync(
            id,
            currentUserId.Value,
            request,
            cancellationToken);

        return Ok(updatedComment);
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
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        await _commentService.DeleteAsync(id, currentUserId.Value, cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/poll/vote")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PollResponse>> VotePoll(
        [FromRoute] Guid id,
        [FromBody] VotePollRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var result = await _pollService.VoteCommentAsync(
            currentUserId.Value,
            id,
            request,
            cancellationToken);

        return result.Status switch
        {
            PollVoteStatus.Success => Ok(result.Poll),
            PollVoteStatus.CommentNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found."),
            PollVoteStatus.PollNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, "Poll was not found."),
            PollVoteStatus.InvalidOption => this.ProblemResponse(StatusCodes.Status400BadRequest, "Poll option is invalid."),
            PollVoteStatus.AlreadyVoted => this.ProblemResponse(StatusCodes.Status409Conflict, "You have already voted in this poll."),
            PollVoteStatus.PollClosed => this.ProblemResponse(StatusCodes.Status409Conflict, "Poll is already closed."),
            _ => this.ProblemResponse(StatusCodes.Status400BadRequest, "Unable to vote in poll.")
        };
    }

    [Authorize]
    [HttpPost("{id:guid}/like")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<CommentLikeStateResponse>> LikeComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.");
        }

        await _likeService.AddCommentLikeAsync(currentUserId.Value, id, cancellationToken);
        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(MapLikeStateResponse(updatedComment));
    }

    [Authorize]
    [HttpPost("{id:guid}/view")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<CommentViewResponse>> RegisterView(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var result = await _commentService.RecordViewAsync(id, currentUserId.Value, cancellationToken);

        return result is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(result);
    }

    [Authorize]
    [HttpDelete("{id:guid}/like")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<CommentLikeStateResponse>> UnlikeComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.");
        }

        await _likeService.RemoveCommentLikeAsync(currentUserId.Value, id, cancellationToken);
        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(MapLikeStateResponse(updatedComment));
    }

    [Authorize]
    [HttpPost("{id:guid}/bookmark")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<CommentBookmarkStateResponse>> BookmarkComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.");
        }

        var wasAdded = await _bookmarkService.AddCommentBookmarkAsync(
            currentUserId.Value,
            id,
            cancellationToken);

        if (!wasAdded)
        {
            var commentAfterFailedBookmark = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId.Value);

            return commentAfterFailedBookmark is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
                : this.ProblemResponse(StatusCodes.Status409Conflict, "You have already bookmarked this comment.");
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(MapBookmarkStateResponse(updatedComment));
    }

    [Authorize]
    [HttpDelete("{id:guid}/bookmark")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<CommentBookmarkStateResponse>> UnbookmarkComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.");
        }

        var wasRemoved = await _bookmarkService.RemoveCommentBookmarkAsync(
            currentUserId.Value,
            id,
            cancellationToken);

        if (!wasRemoved)
        {
            var commentAfterFailedUnbookmark = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId.Value);

            return commentAfterFailedUnbookmark is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
                : this.ProblemResponse(StatusCodes.Status404NotFound, "Bookmark was not found.");
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(MapBookmarkStateResponse(updatedComment));
    }

    [Authorize]
    [HttpPost("{id:guid}/repost")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<CommentRepostStateResponse>> RepostComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.");
        }

        var wasAdded = await _repostService.AddCommentRepostAsync(
            currentUserId.Value,
            id,
            cancellationToken);

        if (!wasAdded)
        {
            var commentAfterFailedRepost = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId.Value);

            return commentAfterFailedRepost is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
                : this.ProblemResponse(StatusCodes.Status409Conflict, "You have already reposted this comment.");
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
            : Ok(MapRepostStateResponse(updatedComment));
    }

    [Authorize]
    [HttpDelete("{id:guid}/repost")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<CommentRepostStateResponse>> UnrepostComment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        if (currentComment is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.");
        }

        var wasRemoved = await _repostService.RemoveCommentRepostAsync(
            currentUserId.Value,
            id,
            cancellationToken);

        if (!wasRemoved)
        {
            var commentAfterFailedUndo = await _commentService.GetByIdAsync(
                id,
                cancellationToken,
                currentUserId.Value);

            return commentAfterFailedUndo is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
                : this.ProblemResponse(StatusCodes.Status404NotFound, "Repost was not found.");
        }

        var updatedComment = await _commentService.GetByIdAsync(
            id,
            cancellationToken,
            currentUserId.Value);

        return updatedComment is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Comment was not found.")
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
