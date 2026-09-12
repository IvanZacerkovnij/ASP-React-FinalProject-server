using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Api.Extensions;
using Threads.Api.Requests.Users;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.DTOs.Bookmarks;
using Threads.Application.DTOs.Likes;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Reposts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;
    private readonly IAuthService _authService;
    private readonly ICommentService _commentService;
    
    public MeController(
        IUserService userService,
        IPostService postService,
        IAuthService authService,
        ICommentService commentService)
    {
        _userService = userService;
        _postService = postService;
        _authService = authService;
        _commentService = commentService;
    }
    
    [HttpGet]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _userService.GetMeAsync(currentUserId.Value, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }
        return Ok(user);
    }
    
    [Consumes("multipart/form-data")]
    [HttpPut]
    public async Task<ActionResult<UserResponse>> UpdateMe(
        [FromForm] UpdateCurrentUserRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        await using var avatarStream = request.Avatar?.OpenReadStream();
        await using var bannerStream = request.Banner?.OpenReadStream();

        var updateRequest = new UpdateUserRequest
        {
            DisplayName = request.DisplayName,
            Bio = request.Bio,
            DateOfBirth = request.DateOfBirth,
            RemoveDateOfBirth = request.RemoveDateOfBirth,
            RemoveLocation = request.RemoveLocation,
            Location = request.Location,
            RemoveAvatar = request.RemoveAvatar,
            RemoveBanner = request.RemoveBanner
        };

        var avatar = request.Avatar is null
            ? null
            : new UserFileUploadRequest
            {
                Content = avatarStream!,
                FileName = request.Avatar.FileName,
                ContentType = request.Avatar.ContentType,
                SizeInBytes = request.Avatar.Length
            };

        var banner = request.Banner is null
            ? null
            : new UserFileUploadRequest
            {
                Content = bannerStream!,
                FileName = request.Banner.FileName,
                ContentType = request.Banner.ContentType,
                SizeInBytes = request.Banner.Length
            };

        try
        {
            var user = await _userService.UpdateAsync(currentUserId.Value, updateRequest, avatar, banner, cancellationToken);

            return user is null
                ? NotFound(new { message = "User was not found." })
                : Ok(user);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
    
    [HttpDelete]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var wasDeleted = await _userService.DeleteAsync(currentUserId.Value, cancellationToken);

        return wasDeleted
            ? NoContent()
            : NotFound(new { message = "User was not found." });
    }
    
    [HttpGet("posts")]
    public async Task<ActionResult<IReadOnlyCollection<PostResponse>>> GetPosted(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }
        
        var posts = await _postService.GetByAuthorIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);
        return Ok(posts);
    }
    
    [HttpGet("likes")]
    public async Task<ActionResult<UserLikesResponse>> GetLiked(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetLikedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        var comments = await _commentService.GetLikedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(new UserLikesResponse
        {
            Posts = posts,
            Comments = comments
        });
    }
    
    [HttpGet("bookmarks")]
    public async Task<ActionResult<UserBookmarksResponse>> GetBookmarked(
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetBookmarkedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        var comments = await _commentService.GetBookmarkedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(new UserBookmarksResponse
        {
            Posts = posts,
            Comments = comments
        });
    }
    
    [HttpGet("reposts")]
    public async Task<ActionResult<UserRepostsResponse>> GetReposted(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var posts = await _postService.GetRepostedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        var comments = await _commentService.GetRepostedByUserIdAsync(
            currentUserId.Value,
            cancellationToken,
            currentUserId.Value);

        return Ok(new UserRepostsResponse
        {
            Posts = posts,
            Comments = comments
        });
    }
    
    [HttpPost("change-password/start")]
    public async Task<IActionResult> StartPasswordChange(
        [FromBody] StartPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();
        
        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }
        
        try
        {
            var result = await _authService.StartPasswordChangeAsync(currentUserId.Value, request, cancellationToken);

            return result.Status switch
            {
                ChangePasswordStatus.ConfirmationCodeSent => Ok(new
                {
                    message = "Password change confirmation code has been sent to your email."
                }),
                ChangePasswordStatus.UserNotFound => NotFound(new { message = "User was not found." }),
                ChangePasswordStatus.InvalidNewPassword => BadRequest(new
                {
                    message = "New password must be different from the current password."
                }),
                ChangePasswordStatus.InvalidCurrentPassword => BadRequest(new
                {
                    message = "Current password is invalid."
                }),
                _ => BadRequest(new { message = "Unable to change password." })
            };
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

    [HttpPost("change-password/confirm")]
    public async Task<IActionResult> ConfirmPasswordChange(
        [FromBody] ConfirmPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetCurrentUserId();

        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        try
        {
            var result = await _authService.ConfirmPasswordChangeAsync(currentUserId.Value, request, cancellationToken);

            return result.Status switch
            {
                ChangePasswordStatus.PasswordChanged => Ok(new
                {
                    message = "Password changed successfully."
                }),
                ChangePasswordStatus.UserNotFound => NotFound(new { message = "User was not found." }),
                ChangePasswordStatus.InvalidConfirmationCode => BadRequest(new { message = "Invalid confirmation code." }),
                ChangePasswordStatus.NoPendingPasswordChange => Conflict(new
                {
                    message = "There is no pending password change request."
                }),
                _ => BadRequest(new { message = "Unable to change password." })
            };
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
}
