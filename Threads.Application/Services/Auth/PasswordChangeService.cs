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

    public PasswordChangeService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthTransaction authTransaction,
        IAuthEmailService authEmailService,
        IAuthCodeHasher authCodeHasher,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _authTransaction = authTransaction;
        _authEmailService = authEmailService;
        _authCodeHasher = authCodeHasher;
        _passwordHasher = passwordHasher;
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
            return CreateResult(ChangePasswordStatus.UserNotFound);
        }

        if (!_passwordHasher.VerifyPassword(normalizedCurrentPassword, user.PasswordHash))
        {
            return CreateResult(ChangePasswordStatus.InvalidCurrentPassword);
        }

        if (_passwordHasher.VerifyPassword(normalizedNewPassword, user.PasswordHash))
        {
            return CreateResult(ChangePasswordStatus.InvalidNewPassword);
        }

        var code = AuthCode.Generate();
        user.PendingPasswordHash = _passwordHasher.HashPassword(normalizedNewPassword);
        AuthCode.SetPasswordChangeCode(user, code, _authCodeHasher);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _authEmailService.SendPasswordChangeCodeAsync(user.Email, code, cancellationToken);

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
            return CreateResult(ChangePasswordStatus.UserNotFound);
        }

        if (user.PendingPasswordHash is null)
        {
            return CreateResult(ChangePasswordStatus.NoPendingPasswordChange);
        }

        if (!AuthCode.IsPasswordChangeCodeValid(user, request.Code, _authCodeHasher))
        {
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
    }

    private static ChangePasswordResult CreateResult(ChangePasswordStatus status)
    {
        return new ChangePasswordResult
        {
            Status = status
        };
    }
}
