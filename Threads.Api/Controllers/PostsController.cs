using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Posts;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Posts;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly ILikeService _likeService;

    public PostsController(IPostService postService, ILikeService likeService)
    {
        _postService = postService;
        _likeService = likeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var posts = await _postService.GetAllAsync(cancellationToken);
        return Ok(posts);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var post = await _postService.GetByIdAsync(id, cancellationToken);

        return post is null
            ? NotFound(new { message = "Post was not found." })
            : Ok(post);
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

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken);

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

        var currentPost = await _postService.GetByIdAsync(id, cancellationToken);

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

        var post = await _postService.CreateAsync(currentUserId.Value, request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
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

        var existingPost = await _postService.GetByIdAsync(id, cancellationToken);

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
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var existingPost = await _postService.GetByIdAsync(id, cancellationToken);

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
