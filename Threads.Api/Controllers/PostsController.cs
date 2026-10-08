using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.ScheduledPosts;
using Threads.Application.DTOs.Versions;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Interfaces.ScheduledPosts;
using Threads.Application.Interfaces.Users;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly ILikeService _likeService;
    private readonly IRepostService _repostService;
    private readonly IPollService _pollService;
    private readonly IUserService _userService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IScheduledPostService _scheduledPostService;

    public PostsController(
        IPostService postService,
        ILikeService likeService,
        IRepostService repostService,
        IPollService pollService,
        IUserService userService,
        IBookmarkService bookmarkService,
        IScheduledPostService scheduledPostService)
    {
        _postService = postService;
        _likeService = likeService;
        _repostService = repostService;
        _pollService = pollService;
        _userService = userService;
        _bookmarkService = bookmarkService;
        _scheduledPostService = scheduledPostService;
    }

    [HttpGet("feed")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetFeed(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var posts = await _postService.GetFeedAsync(cancellationToken, currentUserId);
        return Ok(posts);
    }

    [Authorize]
    [HttpGet("scheduled")]
    public async Task<ActionResult<IReadOnlyCollection<ScheduledPostResponse>>> GetScheduled(
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var scheduledPosts = await _scheduledPostService.GetAsync(
            currentUserId.Value,
            cancellationToken);
        return Ok(scheduledPosts);
    }

    [Authorize]
    [HttpPost("scheduled")]
    public async Task<ActionResult<ScheduledPostResponse>> CreateScheduled(
        [FromBody] CreateScheduledPostRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var scheduledPost = await _scheduledPostService.CreateAsync(
            currentUserId.Value,
            request,
            cancellationToken);
        return Ok(scheduledPost);
    }

    [Authorize]
    [HttpPatch("scheduled/{id:guid}")]
    public async Task<ActionResult<ScheduledPostResponse>> UpdateScheduled(
        [FromRoute] Guid id,
        [FromBody] UpdateScheduledPostRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var scheduledPost = await _scheduledPostService.UpdateAsync(
            id,
            currentUserId.Value,
            request,
            cancellationToken);
        return Ok(scheduledPost);
    }

    [Authorize]
    [HttpDelete("scheduled/{id:guid}")]
    public async Task<IActionResult> DeleteScheduled(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        await _scheduledPostService.DeleteAsync(
            id,
            currentUserId.Value,
            cancellationToken);
        return NoContent();
    }
    

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var post = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        return post is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
            : Ok(post);
    }

    [HttpGet("{id:guid}/edit-history")]
    [EnableRateLimiting(RateLimiterConfigurator.EditHistoryPolicyName)]
    public async Task<ActionResult<EditHistoryResponse<PostResponse>>> GetEditHistory(
        [FromRoute] Guid id,
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        var history = await _postService.GetEditHistoryAsync(
            id,
            pagination,
            cancellationToken,
            currentUserId);

        return history is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
            : Ok(history);
    }

    [Authorize]
    [HttpPost("{id:guid}/view")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PostViewResponse>> RegisterView(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var result = await _postService.RecordViewAsync(id, currentUserId.Value, cancellationToken);

        return result is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
            : Ok(result);
    }

    [Authorize]
    [HttpPost("{id:guid}/like")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PostLikeStateResponse>> LikePost(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.");
        }

        await _likeService.AddPostLikeAsync(currentUserId.Value, id, cancellationToken);
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
            : Ok(MapLikeStateResponse(updatedPost));
    }

    [Authorize]
    [HttpPost("{id:guid}/repost")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PostRepostStateResponse>> RepostPost(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.");
        }

        var wasAdded = await _repostService.AddPostRepostAsync(currentUserId.Value, id, cancellationToken);

        if (!wasAdded)
        {
            var postAfterFailedRepost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedRepost is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
                : this.ProblemResponse(StatusCodes.Status409Conflict, "You have already reposted this post.");
        }

        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
            : Ok(MapRepostStateResponse(updatedPost));
    }

    [Authorize]
    [HttpDelete("{id:guid}/repost")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PostRepostStateResponse>> UndoRepostPost(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.");
        }

        var wasRemoved = await _repostService.RemovePostRepostAsync(currentUserId.Value, id, cancellationToken);

        if (!wasRemoved)
        {
            var postAfterFailedUndo = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedUndo is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
                : this.ProblemResponse(StatusCodes.Status404NotFound, "Repost was not found.");
        }

        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
            : Ok(MapRepostStateResponse(updatedPost));
    }

    [Authorize]
    [HttpDelete("{id:guid}/like")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PostLikeStateResponse>> UnlikePost(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.");
        }

        await _likeService.RemovePostLikeAsync(currentUserId.Value, id, cancellationToken);
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
            : Ok(MapLikeStateResponse(updatedPost));
    }

    [Authorize]
    [HttpPost("{id:guid}/bookmark")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PostBookmarkStateResponse>> BookmarkPost(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }
        
        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.");
        }
        
        var wasAdded = await _bookmarkService.AddPostBookmarkAsync(currentUserId.Value, id, cancellationToken);
        if (!wasAdded)
        {
            var postAfterFailedBookmark = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedBookmark is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
                : this.ProblemResponse(StatusCodes.Status409Conflict, "You have already bookmarked this post.");
        }
        
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);
        
        return updatedPost is null 
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found."):
            Ok(MapBookmarkStateResponse(updatedPost));
    }
    
    [Authorize]
    [HttpDelete("{id:guid}/bookmark")]
    [EnableRateLimiting(RateLimiterConfigurator.InteractionPolicyName)]
    public async Task<ActionResult<PostBookmarkStateResponse>> UnBookmarkPost(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }
        
        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.");
        }
        
        var wasRemoved = await _bookmarkService.RemovePostBookmarkAsync(currentUserId.Value, id, cancellationToken);
        if (!wasRemoved)
        {
            var postAfterFailedBookmark = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedBookmark is null
                ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found.")
                : this.ProblemResponse(StatusCodes.Status404NotFound, "Bookmark was not found.");
        }
        
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);
        
        return updatedPost is null 
            ? this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found."):
            Ok(MapBookmarkStateResponse(updatedPost));
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

        var result = await _pollService.VoteAsync(currentUserId.Value, id, request, cancellationToken);

        return result.Status switch
        {
            PollVoteStatus.Success => Ok(result.Poll),
            PollVoteStatus.PostNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, "Post was not found."),
            PollVoteStatus.PollNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, "Poll was not found."),
            PollVoteStatus.InvalidOption => this.ProblemResponse(StatusCodes.Status400BadRequest, "Poll option is invalid."),
            PollVoteStatus.AlreadyVoted => this.ProblemResponse(StatusCodes.Status409Conflict, "You have already voted in this poll."),
            PollVoteStatus.PollClosed => this.ProblemResponse(StatusCodes.Status409Conflict, "Poll is already closed."),
            _ => this.ProblemResponse(StatusCodes.Status400BadRequest, "Unable to vote in poll.")
        };
    }

    [Authorize]
    [HttpPost]
    [EnableRateLimiting(RateLimiterConfigurator.PostCreationPolicyName)]
    public async Task<ActionResult<PostResponse>> Create(
        [FromBody] CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var post = await _postService.CreateAsync(currentUserId.Value, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostResponse>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdatePostRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return this.ProblemResponse(StatusCodes.Status401Unauthorized, "Invalid token claims.");
        }

        var updatedPost = await _postService.UpdateAsync(
            id,
            currentUserId.Value,
            request,
            cancellationToken);

        return Ok(updatedPost);
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

        await _postService.DeleteAsync(id, currentUserId.Value, cancellationToken);

        return NoContent();
    }

    private static PostLikeStateResponse MapLikeStateResponse(PostResponse post)
    {
        return new PostLikeStateResponse
        {
            LikedByMe = post.IsLikedByCurrentUser,
            LikesCount = post.LikesCount
        };
    }

    private static PostRepostStateResponse MapRepostStateResponse(PostResponse post)
    {
        return new PostRepostStateResponse
        {
            RepostedByMe = post.IsRepostedByCurrentUser,
            RepostsCount = post.RepostsCount
        };
    }

    private static PostBookmarkStateResponse MapBookmarkStateResponse(PostResponse post)
    {
        return new PostBookmarkStateResponse()
        {
            BookmarkedByMe = post.IsBookmarkedByCurrentUser,
            BookmarksCount = post.BookmarksCount
        };
    }
}
