using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Threads.Api.Extensions;
using Threads.Api.Responses;
using Threads.Application.DTOs.Pagination;
using Threads.Api.Requests.Users;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.DTOs.Bookmarks;
using Threads.Application.DTOs.Likes;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Reposts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Interfaces.Users;
using Threads.Infrastructure.Services;

namespace Threads.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IPostService _postService;
    private readonly IAuthService _authService;
    private readonly IBookmarkService _bookmarkService;
    private readonly ILikeService _likeService;
    private readonly IRepostService _repostService;
    
    public MeController(
        IUserService userService,
        IPostService postService,
        IAuthService authService,
        IBookmarkService bookmarkService,
        ILikeService likeService,
        IRepostService repostService)
    {
        _userService = userService;
        _postService = postService;
        _authService = authService;
        _bookmarkService = bookmarkService;
        _likeService = likeService;
        _repostService = repostService;
    }
    
    [HttpGet]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var user = await _userService.GetMeAsync(currentUserId, cancellationToken);

        if (user is null)
        {
            return this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound);
        }
        return Ok(user);
    }
    
    [Consumes("multipart/form-data")]
    [HttpPut]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> UpdateMe(
        [FromForm] UpdateCurrentUserRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        await using var avatarStream = request.Avatar?.OpenReadStream();
        await using var bannerStream = request.Banner?.OpenReadStream();

        var updateRequest = new UpdateUserRequest
        {
            DisplayName = request.DisplayName,
            Bio = request.Bio,
            DateOfBirth = request.BirthDate,
            BirthDateVisibility = request.BirthDateVisibility,
            BirthYearVisibility = request.BirthYearVisibility,
            RemoveDateOfBirth = request.RemoveBirthDate,
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

        var user = await _userService.UpdateAsync(currentUserId, updateRequest, avatar, banner, cancellationToken);

        return user is null
            ? this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound)
            : Ok(user);
    }
    
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var wasDeleted = await _userService.DeleteAsync(currentUserId, cancellationToken);

        return wasDeleted
            ? NoContent()
            : this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound);
    }
    
    [HttpGet("posts")]
    [ProducesResponseType<CursorPageResponse<PostResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPageResponse<PostResponse>>> GetPosted(
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();
        
        var posts = await _postService.GetByAuthorIdAsync(
            currentUserId,
            pagination,
            cancellationToken,
            currentUserId);
        return Ok(posts);
    }
    
    [HttpGet("likes")]
    [ProducesResponseType<UserLikesPageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserLikesPageResponse>> GetLiked(
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var likes = await _likeService.GetByUserIdAsync(
            currentUserId,
            pagination,
            cancellationToken,
            currentUserId);

        return Ok(likes);
    }
    
    [HttpGet("bookmarks")]
    [ProducesResponseType<UserBookmarksPageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserBookmarksPageResponse>> GetBookmarked(
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var bookmarks = await _bookmarkService.GetByUserIdAsync(
            currentUserId,
            pagination,
            cancellationToken,
            currentUserId);

        return Ok(bookmarks);
    }
    
    [HttpGet("reposts")]
    [ProducesResponseType<UserRepostsPageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserRepostsPageResponse>> GetReposted(
        [FromQuery] CursorPageRequest pagination,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var reposts = await _repostService.GetByUserIdAsync(
            currentUserId,
            pagination,
            cancellationToken,
            currentUserId);

        return Ok(reposts);
    }
    
    [HttpPost("change-password/start")]
    [EnableRateLimiting(RateLimiterConfigurator.ChangePasswordStartPolicyName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> StartPasswordChange(
        [FromBody] StartPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();
        
        var result = await _authService.StartPasswordChangeAsync(currentUserId, request, cancellationToken);

        return result.Status switch
        {
            ChangePasswordStatus.ConfirmationCodeSent => Ok(new
            {
                message = "Password change confirmation code has been sent to your email."
            }),
            ChangePasswordStatus.UserNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound),
            ChangePasswordStatus.InvalidNewPassword => this.ProblemResponse(StatusCodes.Status400BadRequest, "New password must be different from the current password."),
            ChangePasswordStatus.InvalidCurrentPassword => this.ProblemResponse(StatusCodes.Status400BadRequest, "Current password is invalid."),
            _ => this.ProblemResponse(StatusCodes.Status400BadRequest, "Unable to change password.")
        };
    }

    [HttpPost("change-password/confirm")]
    [EnableRateLimiting(RateLimiterConfigurator.ChangePasswordConfirmPolicyName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPasswordChange(
        [FromBody] ConfirmPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.GetRequiredCurrentUserId();

        var result = await _authService.ConfirmPasswordChangeAsync(currentUserId, request, cancellationToken);

        return result.Status switch
        {
            ChangePasswordStatus.PasswordChanged => Ok(new
            {
                message = "Password changed successfully."
            }),
            ChangePasswordStatus.UserNotFound => this.ProblemResponse(StatusCodes.Status404NotFound, ApiErrorMessages.UserNotFound),
            ChangePasswordStatus.InvalidConfirmationCode => this.ProblemResponse(StatusCodes.Status400BadRequest, "Invalid confirmation code."),
            ChangePasswordStatus.NoPendingPasswordChange => this.ProblemResponse(StatusCodes.Status409Conflict, "There is no pending password change request."),
            _ => this.ProblemResponse(StatusCodes.Status400BadRequest, "Unable to change password.")
        };
    }
}
