using Microsoft.AspNetCore.Mvc;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Interfaces.Auth;

namespace Threads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.RegisterAsync(request, cancellationToken);
        return Ok(new { message = "Verification code has been sent to your email." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);

        return response is null
            ? Unauthorized(new { message = "Invalid credentials." })
            : Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);

        return response is null
            ? Unauthorized(new { message = "Invalid refresh token." })
            : Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var wasLoggedOut = await _authService.LogoutAsync(request, cancellationToken);

        return wasLoggedOut
            ? NoContent()
            : Unauthorized(new { message = "Invalid refresh token." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(new { message = "If the email exists, a reset code has been sent." });
    }

    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode(
        [FromBody] VerifyResetCodeRequest request,
        CancellationToken cancellationToken)
    {
        var isValid = await _authService.VerifyResetCodeAsync(request, cancellationToken);

        return isValid
            ? Ok(new { message = "Reset code is valid." })
            : BadRequest(new { message = "Reset code is invalid or expired." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var wasReset = await _authService.ResetPasswordAsync(request, cancellationToken);

        return wasReset
            ? Ok(new { message = "Password reset successfully." })
            : BadRequest(new { message = "Reset code is invalid or expired." });
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponse>> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyEmailAsync(request, cancellationToken);

        return response is null
            ? BadRequest(new { message = "Verification code is invalid or expired." })
            : Ok(response);
    }
    
    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerifyEmail(
        [FromBody] ResendVerificationCodeRequest request,
        CancellationToken cancellationToken)
    {
        var wasSent = await _authService.ResendVerificationCodeAsync(request, cancellationToken);

        return wasSent
            ? Ok(new { message = "Verification code has been sent to your email." })
            : NotFound(new { message = "Pending registration was not found or expired." });
    }
}
