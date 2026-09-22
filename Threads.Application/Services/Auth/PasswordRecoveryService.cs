using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Auth;

public sealed class PasswordRecoveryService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthEmailService _authEmailService;
    private readonly IAuthCodeHasher _authCodeHasher;
    private readonly IPasswordHasher _passwordHasher;
    private readonly PasswordChangeService _passwordChangeService;

    public PasswordRecoveryService(
        IUserRepository userRepository,
        IAuthEmailService authEmailService,
        IAuthCodeHasher authCodeHasher,
        IPasswordHasher passwordHasher,
        PasswordChangeService passwordChangeService)
    {
        _userRepository = userRepository;
        _authEmailService = authEmailService;
        _authCodeHasher = authCodeHasher;
        _passwordHasher = passwordHasher;
        _passwordChangeService = passwordChangeService;
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            return;
        }

        var code = AuthCode.Generate();
        user.PendingPasswordHash = null;
        AuthCode.SetPasswordResetCode(user, code, _authCodeHasher);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _authEmailService.SendPasswordResetCodeAsync(user.Email, code, cancellationToken);
    }

    public async Task<bool> VerifyResetCodeAsync(
        VerifyResetCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AuthInputNormalizer.NormalizeEmail(request.Email);
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        return AuthCode.IsPasswordResetCodeValid(user, request.Code, _authCodeHasher);
    }

    public async Task<bool> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AuthInputNormalizer.NormalizeEmail(request.Email);
        var normalizedPassword = AuthInputNormalizer.NormalizePassword(
            request.NewPassword,
            nameof(request.NewPassword));
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (!AuthCode.IsPasswordResetCodeValid(user, request.Code, _authCodeHasher))
        {
            return false;
        }

        var newPasswordHash = _passwordHasher.HashPassword(normalizedPassword);
        await _passwordChangeService.ApplyAsync(user!, newPasswordHash, cancellationToken);
        return true;
    }

    private async Task<User?> GetActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = AuthInputNormalizer.NormalizeEmail(email);
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        return user is { IsActive: true }
            ? user
            : null;
    }
}
