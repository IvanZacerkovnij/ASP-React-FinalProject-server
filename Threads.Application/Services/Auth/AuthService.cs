using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Interfaces.Auth;

namespace Threads.Application.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly RegistrationService _registrationService;
    private readonly SessionService _sessionService;
    private readonly PasswordRecoveryService _passwordRecoveryService;
    private readonly PasswordChangeService _passwordChangeService;

    public AuthService(
        RegistrationService registrationService,
        SessionService sessionService,
        PasswordRecoveryService passwordRecoveryService,
        PasswordChangeService passwordChangeService)
    {
        _registrationService = registrationService;
        _sessionService = sessionService;
        _passwordRecoveryService = passwordRecoveryService;
        _passwordChangeService = passwordChangeService;
    }

    public Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        return _registrationService.RegisterAsync(request, cancellationToken);
    }

    public Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return _sessionService.LoginAsync(request, cancellationToken);
    }

    public Task<AuthResponse?> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        return _sessionService.RefreshTokenAsync(request, cancellationToken);
    }

    public Task<bool> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        return _sessionService.LogoutAsync(request, cancellationToken);
    }

    public Task<AuthResponse?> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        return _registrationService.VerifyEmailAsync(request, cancellationToken);
    }

    public Task<bool> ResendVerificationCodeAsync(
        ResendVerificationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        return _registrationService.ResendVerificationCodeAsync(request, cancellationToken);
    }

    public Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        return _passwordRecoveryService.ForgotPasswordAsync(request, cancellationToken);
    }

    public Task<bool> VerifyResetCodeAsync(
        VerifyResetCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        return _passwordRecoveryService.VerifyResetCodeAsync(request, cancellationToken);
    }

    public Task<bool> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        return _passwordRecoveryService.ResetPasswordAsync(request, cancellationToken);
    }

    public Task<ChangePasswordResult> StartPasswordChangeAsync(
        Guid userId,
        StartPasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        return _passwordChangeService.StartPasswordChangeAsync(userId, request, cancellationToken);
    }

    public Task<ChangePasswordResult> ConfirmPasswordChangeAsync(
        Guid userId,
        ConfirmPasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        return _passwordChangeService.ConfirmPasswordChangeAsync(userId, request, cancellationToken);
    }
}
