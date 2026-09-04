using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Auth;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Users;
using Threads.Application.DTOs.Users;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authService.RegisterAsync(request, cancellationToken);
            return Ok(new { message = "Verification code has been sent to your email." });
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

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);

        return response is null
            ? Unauthorized(new { message = "Invalid credentials." })
            : Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);

        return response is null
            ? Unauthorized(new { message = "Invalid refresh token." })
            : Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var wasLoggedOut = await _authService.LogoutAsync(request, cancellationToken);

        return wasLoggedOut
            ? NoContent()
            : Unauthorized(new { message = "Invalid refresh token." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _authService.ForgotPasswordAsync(request, cancellationToken);
            return Ok(new { message = "If the email exists, a reset code has been sent." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode(
        VerifyResetCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var isValid = await _authService.VerifyResetCodeAsync(request, cancellationToken);

            return isValid
                ? Ok(new { message = "Reset code is valid." })
                : BadRequest(new { message = "Reset code is invalid or expired." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var wasReset = await _authService.ResetPasswordAsync(request, cancellationToken);

            return wasReset
                ? Ok(new { message = "Password reset successfully." })
                : BadRequest(new { message = "Reset code is invalid or expired." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponse>> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.VerifyEmailAsync(request, cancellationToken);

            return response is null
                ? BadRequest(new { message = "Verification code is invalid or expired." })
                : Ok(response);
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
    
    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerifyEmail(
        ResendVerificationCodeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var wasSent = await _authService.ResendVerificationCodeAsync(request, cancellationToken);

            return wasSent
                ? Ok(new { message = "Verification code has been sent to your email." })
                : NotFound(new { message = "Pending registration was not found or expired." });
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

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        try
        {
            var result = await _authService.ChangePasswordAsync(parsedUserId, request, cancellationToken);

            return result.Status switch
            {
                ChangePasswordStatus.ConfirmationCodeSent => Ok(new
                {
                    message = "Password change confirmation code has been sent to your email."
                }),
                ChangePasswordStatus.PasswordChanged => Ok(new
                {
                    message = "Password changed successfully."
                }),
                ChangePasswordStatus.UserNotFound => NotFound(new { message = "User was not found." }),
                ChangePasswordStatus.InvalidCurrentPassword => BadRequest(new
                {
                    message = "Current password is invalid."
                }),
                ChangePasswordStatus.InvalidConfirmationCode => BadRequest(new
                {
                    message = "Confirmation code is invalid or expired."
                }),
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

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _userService.GetByIdAsync(parsedUserId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { message = "User was not found." });
        }
        return Ok(user);
    }
}
