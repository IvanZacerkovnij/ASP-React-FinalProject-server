using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Interfaces.Users;
using Threads.Infrastracture.Services;

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

    public PostsController(
        IPostService postService,
        ILikeService likeService,
        IRepostService repostService,
        IPollService pollService,
        IUserService userService,
        IBookmarkService bookmarkService)
    {
        _postService = postService;
        _likeService = likeService;
        _repostService = repostService;
        _pollService = pollService;
        _userService = userService;
        _bookmarkService = bookmarkService;
    }

    [HttpGet("feed")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetFeed(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var posts = await _postService.GetFeedAsync(cancellationToken, currentUserId);
        return Ok(posts);
    }
    

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        var post = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        return post is null
            ? NotFound(new { message = "Post was not found." })
            : Ok(post);
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
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var result = await _postService.RecordViewAsync(id, currentUserId.Value, cancellationToken);

        return result is null
            ? NotFound(new { message = "Post was not found." })
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
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }

        await _likeService.AddLikeAsync(currentUserId.Value, id, cancellationToken);
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? NotFound(new { message = "Post was not found." })
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
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }

        var wasAdded = await _repostService.AddRepostAsync(currentUserId.Value, id, cancellationToken);

        if (!wasAdded)
        {
            var postAfterFailedRepost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedRepost is null
                ? NotFound(new { message = "Post was not found." })
                : Conflict(new { message = "You have already reposted this post." });
        }

        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? NotFound(new { message = "Post was not found." })
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
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }

        var wasRemoved = await _repostService.RemoveRepostAsync(currentUserId.Value, id, cancellationToken);

        if (!wasRemoved)
        {
            var postAfterFailedUndo = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedUndo is null
                ? NotFound(new { message = "Post was not found." })
                : NotFound(new { message = "Repost was not found." });
        }

        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? NotFound(new { message = "Post was not found." })
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
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }

        await _likeService.RemoveLikeAsync(currentUserId.Value, id, cancellationToken);
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

        return updatedPost is null
            ? NotFound(new { message = "Post was not found." })
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
            return Unauthorized(new { message = "Invalid token claims." });
        }
        
        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }
        
        var wasAdded = await _bookmarkService.AddBookmarkAsync(currentUserId.Value, id, cancellationToken);
        if (!wasAdded)
        {
            var postAfterFailedBookmark = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedBookmark is null
                ? NotFound(new { message = "Post was not found." })
                : Conflict(new { message = "You have already bookmarked this post." });
        }
        
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);
        
        return updatedPost is null 
            ? NotFound(new { message = "Post was not found." }):
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
            return Unauthorized(new { message = "Invalid token claims." });
        }
        
        var currentPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (currentPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }
        
        var wasRemoved = await _bookmarkService.RemoveBookmarkAsync(currentUserId.Value, id, cancellationToken);
        if (!wasRemoved)
        {
            var postAfterFailedBookmark = await _postService.GetByIdAsync(id, cancellationToken, currentUserId.Value);

            return postAfterFailedBookmark is null
                ? NotFound(new { message = "Post was not found." })
                : NotFound(new { message = "Bookmark was not found." });
        }
        
        var updatedPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);
        
        return updatedPost is null 
            ? NotFound(new { message = "Post was not found." }):
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
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var result = await _pollService.VoteAsync(currentUserId.Value, id, request, cancellationToken);

        return result.Status switch
        {
            PollVoteStatus.Success => Ok(result.Poll),
            PollVoteStatus.PostNotFound => NotFound(new { message = "Post was not found." }),
            PollVoteStatus.PollNotFound => NotFound(new { message = "Poll was not found." }),
            PollVoteStatus.InvalidOption => BadRequest(new { message = "Poll option is invalid." }),
            PollVoteStatus.AlreadyVoted => Conflict(new { message = "You have already voted in this poll." }),
            PollVoteStatus.PollClosed => Conflict(new { message = "Poll is already closed." }),
            _ => BadRequest(new { message = "Unable to vote in poll." })
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
            return Unauthorized(new { message = "Invalid token claims." });
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
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var existingPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (existingPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }

        if (existingPost.Author.Id != currentUserId.Value)
        {
            return Forbid();
        }

        var updatedPost = await _postService.UpdateAsync(id, request, cancellationToken);

        return updatedPost is null
            ? NotFound(new { message = "Post was not found." })
            : Ok(updatedPost);
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

        var existingPost = await _postService.GetByIdAsync(id, cancellationToken, currentUserId);

        if (existingPost is null)
        {
            return NotFound(new { message = "Post was not found." });
        }

        if (existingPost.Author.Id != currentUserId.Value)
        {
            return Forbid();
        }

        await _postService.DeleteAsync(id, cancellationToken);

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
