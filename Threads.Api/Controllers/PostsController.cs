using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly ILikeService _likeService;
    private readonly IPollService _pollService;

    public PostsController(IPostService postService, ILikeService likeService, IPollService pollService)
    {
        _postService = postService;
        _likeService = likeService;
        _pollService = pollService;
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
    public async Task<ActionResult> LikePost(Guid id, CancellationToken cancellationToken)
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

        var wasAdded = await _likeService.AddLikeAsync(currentUserId.Value, id, cancellationToken);

        return wasAdded
            ? Ok(new { message = "Post liked successfully." })
            : Conflict(new { message = "Post is already liked." });
    }

    [Authorize]
    [HttpDelete("{id:guid}/like")]
    public async Task<ActionResult> UnlikePost(Guid id, CancellationToken cancellationToken)
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

        var wasRemoved = await _likeService.RemoveLikeAsync(currentUserId.Value, id, cancellationToken);

        return wasRemoved
            ? NoContent()
            : NotFound(new { message = "Like was not found." });
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
}
