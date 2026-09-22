using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Auth;

public sealed class SessionService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly RefreshTokenManager _refreshTokenManager;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        RefreshTokenManager refreshTokenManager,
        ILogger<SessionService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenManager = refreshTokenManager;
        _logger = logger;
    }

    public async Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentity = AuthInputNormalizer.NormalizeRequired(
            request.EmailOrUsername,
            "Email or username is required.");
        var normalizedPassword = AuthInputNormalizer.NormalizePassword(
            request.Password,
            nameof(request.Password));
        var user = await GetUserByEmailOrUsernameAsync(normalizedIdentity, cancellationToken);

        if (!CanUserLogin(user) ||
            !_passwordHasher.VerifyPassword(normalizedPassword, user!.PasswordHash))
        {
            _logger.LogWarning("Login failed for user {UserId}", user?.Id);
            return null;
        }

        var response = await CreateAuthResponseAsync(user, cancellationToken);
        _logger.LogInformation("User {UserId} logged in", user.Id);
        return response;
    }

    public async Task<AuthResponse?> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentToken = await _refreshTokenManager.GetActiveAsync(
            request.RefreshToken,
            cancellationToken);

        if (currentToken is null)
        {
            _logger.LogWarning("Refresh token validation failed");
            return null;
        }

        var newRefreshToken = await _refreshTokenManager.RotateAsync(
            currentToken,
            cancellationToken);

        if (newRefreshToken is null)
        {
            _logger.LogWarning(
                "Refresh token rotation failed for user {UserId} and token record {RefreshTokenId}",
                currentToken.UserId,
                currentToken.Id);
            return null;
        }

        _logger.LogInformation("Refresh token rotated for user {UserId}", currentToken.UserId);
        return CreateAuthResponse(currentToken.User, newRefreshToken);
    }

    public async Task<bool> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await _refreshTokenManager.GetStoredAsync(
            request.RefreshToken,
            cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
        {
            _logger.LogWarning("Logout attempted with an invalid refresh token");
            return false;
        }

        await _refreshTokenManager.RevokeAsync(refreshToken, cancellationToken);
        _logger.LogInformation("User {UserId} logged out", refreshToken.UserId);
        return true;
    }

    internal async Task<AuthResponse> CreateAuthResponseAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await _refreshTokenManager.IssueAsync(user.Id, cancellationToken);
        return CreateAuthResponse(user, refreshToken);
    }

    private async Task<User?> GetUserByEmailOrUsernameAsync(
        string emailOrUsername,
        CancellationToken cancellationToken)
    {
        if (emailOrUsername.Contains('@'))
        {
            return await _userRepository.GetByEmailAsync(
                AuthInputNormalizer.NormalizeEmail(emailOrUsername),
                cancellationToken);
        }

        return await _userRepository.GetByUsernameAsync(
            AuthInputNormalizer.NormalizeUsername(emailOrUsername),
            cancellationToken);
    }

    private static bool CanUserLogin(User? user)
    {
        return user is { IsActive: true, IsVerified: true };
    }

    private AuthResponse CreateAuthResponse(User user, string refreshToken)
    {
        return new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            AccessToken = _tokenService.GenerateAccessToken(user),
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = _tokenService.GetAccessTokenExpiresAtUtc()
        };
    }
}
