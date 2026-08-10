using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Interfaces.Users;

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

    public PostsController(
        IPostService postService,
        ILikeService likeService,
        IRepostService repostService,
        IPollService pollService,
        IUserService userService)
    {
        _postService = postService;
        _likeService = likeService;
        _repostService = repostService;
        _pollService = pollService;
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var posts = await _postService.GetAllAsync(cancellationToken, GetCurrentUserId());
        return Ok(posts);
    }

    [HttpGet("feed")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetFeed(CancellationToken cancellationToken)
    {
        var posts = await _postService.GetFeedAsync(cancellationToken, GetCurrentUserId());
        return Ok(posts);
    }

    [Authorize]
    [HttpGet("liked")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetLiked(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetLikedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(posts);
    }

    [HttpGet("user/{username}")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetByUsername(
        string username,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByUsernameAsync(username, cancellationToken, GetCurrentUserId());

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }

        var posts = await _postService.GetByAuthorIdAsync(user.Id, cancellationToken, GetCurrentUserId());

        return Ok(posts);
    }

    [Authorize]
    [HttpGet("reposted")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetReposted(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetRepostedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(posts);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var post = await _postService.GetByIdAsync(id, cancellationToken, GetCurrentUserId());

        return post is null
            ? NotFound(new { message = "Post was not found." })
            : Ok(post);
    }

    [Authorize]
    [HttpPost("{id:guid}/view")]
    public async Task<ActionResult<PostViewResponse>> RegisterView(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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
    public async Task<ActionResult<PostLikeStateResponse>> LikePost(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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
    public async Task<ActionResult<PostRepostStateResponse>> RepostPost(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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
    public async Task<ActionResult<PostRepostStateResponse>> UndoRepostPost(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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
    public async Task<ActionResult<PostLikeStateResponse>> UnlikePost(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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
    [HttpPost("{id:guid}/poll/vote")]
    public async Task<ActionResult<PollResponse>> VotePoll(
        Guid id,
        VotePollRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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
    public async Task<ActionResult<PostResponse>> Create(
        CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        try
        {
            var post = await _postService.CreateAsync(currentUserId.Value, request, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PostResponse>> Update(
        Guid id,
        UpdatePostRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

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

        try
        {
            var updatedPost = await _postService.UpdateAsync(id, request, cancellationToken);

            return updatedPost is null
                ? NotFound(new { message = "Post was not found." })
                : Ok(updatedPost);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
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

    private Guid? GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
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
}
