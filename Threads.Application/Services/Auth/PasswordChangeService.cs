using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Auth;

public sealed class PasswordChangeService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthTransaction _authTransaction;
    private readonly IAuthEmailService _authEmailService;
    private readonly IAuthCodeHasher _authCodeHasher;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<PasswordChangeService> _logger;

    public PasswordChangeService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthTransaction authTransaction,
        IAuthEmailService authEmailService,
        IAuthCodeHasher authCodeHasher,
        IPasswordHasher passwordHasher,
        ILogger<PasswordChangeService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _authTransaction = authTransaction;
        _authEmailService = authEmailService;
        _authCodeHasher = authCodeHasher;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ChangePasswordResult> StartPasswordChangeAsync(
        Guid userId,
        StartPasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrentPassword = AuthInputNormalizer.NormalizePassword(
            request.CurrentPassword,
            nameof(request.CurrentPassword));
        var normalizedNewPassword = AuthInputNormalizer.NormalizePassword(
            request.NewPassword,
            nameof(request.NewPassword));
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Password change requested for missing or inactive user {UserId}", userId);
            return CreateResult(ChangePasswordStatus.UserNotFound);
        }

        if (!_passwordHasher.VerifyPassword(normalizedCurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Password change rejected for user {UserId}: invalid current password", userId);
            return CreateResult(ChangePasswordStatus.InvalidCurrentPassword);
        }

        if (_passwordHasher.VerifyPassword(normalizedNewPassword, user.PasswordHash))
        {
            _logger.LogInformation("Password change rejected for user {UserId}: password was not changed", userId);
            return CreateResult(ChangePasswordStatus.InvalidNewPassword);
        }

        var code = AuthCode.Generate();
        user.PendingPasswordHash = _passwordHasher.HashPassword(normalizedNewPassword);
        AuthCode.SetPasswordChangeCode(user, code, _authCodeHasher);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _authEmailService.SendPasswordChangeCodeAsync(user.Email, code, cancellationToken);
        _logger.LogInformation("Password change confirmation code sent for user {UserId}", userId);

        return CreateResult(ChangePasswordStatus.ConfirmationCodeSent);
    }

    public async Task<ChangePasswordResult> ConfirmPasswordChangeAsync(
        Guid userId,
        ConfirmPasswordChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Password change confirmation requested for missing or inactive user {UserId}", userId);
            return CreateResult(ChangePasswordStatus.UserNotFound);
        }

        if (user.PendingPasswordHash is null)
        {
            _logger.LogWarning("Password change confirmation has no pending change for user {UserId}", userId);
            return CreateResult(ChangePasswordStatus.NoPendingPasswordChange);
        }

        if (!AuthCode.IsPasswordChangeCodeValid(user, request.Code, _authCodeHasher))
        {
            _logger.LogWarning("Password change confirmation code is invalid for user {UserId}", userId);
            return CreateResult(ChangePasswordStatus.InvalidConfirmationCode);
        }

        await ApplyAsync(user, user.PendingPasswordHash, cancellationToken);
        return CreateResult(ChangePasswordStatus.PasswordChanged);
    }

    internal async Task ApplyAsync(
        User user,
        string newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        await _authTransaction.ExecuteAsync(async token =>
        {
            user.PasswordHash = newPasswordHash;
            user.PendingPasswordHash = null;
            AuthCode.ClearPasswordResetCode(user);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _userRepository.UpdateAsync(user, token);
            await _refreshTokenRepository.RevokeAllByUserIdAsync(
                user.Id,
                DateTimeOffset.UtcNow,
                token);
        }, cancellationToken);

        _logger.LogInformation("Password changed and active sessions revoked for user {UserId}", user.Id);
    }

    private static ChangePasswordResult CreateResult(ChangePasswordStatus status)
    {
        return new ChangePasswordResult
        {
            Status = status
        };
    }
}
