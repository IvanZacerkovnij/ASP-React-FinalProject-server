using System.Security.Cryptography;
using System.Text;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Auth;

public sealed class RefreshTokenManager
{
    private const int RefreshTokenLifetimeDays = 7;

    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;

    public RefreshTokenManager(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
    }

    public async Task<RefreshToken?> GetStoredAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var normalizedToken = AuthInputNormalizer.NormalizeRequired(
            refreshToken,
            "Refresh token is required.");
        var tokenHash = Hash(normalizedToken);

        return await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
    }

    public async Task<RefreshToken?> GetActiveAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var storedToken = await GetStoredAsync(refreshToken, cancellationToken);

        if (storedToken is null || !storedToken.IsActive || !storedToken.User.IsActive)
        {
            return null;
        }

        return storedToken;
    }

    public async Task<string> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenEntity = new RefreshToken
        {
            TokenHash = Hash(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenLifetimeDays),
            UserId = userId
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        return refreshToken;
    }

    public async Task<string?> RotateAsync(
        RefreshToken currentToken,
        CancellationToken cancellationToken = default)
    {
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var revokedAt = DateTimeOffset.UtcNow;
        var newRefreshTokenEntity = new RefreshToken
        {
            TokenHash = Hash(newRefreshToken),
            ExpiresAt = revokedAt.AddDays(RefreshTokenLifetimeDays),
            UserId = currentToken.UserId
        };

        var wasRotated = await _refreshTokenRepository.TryRotateAsync(
            currentToken.Id,
            newRefreshTokenEntity,
            revokedAt,
            cancellationToken);

        return wasRotated
            ? newRefreshToken
            : null;
    }

    public async Task RevokeAsync(
        RefreshToken refreshToken,
        CancellationToken cancellationToken = default)
    {
        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);
    }

    private static string Hash(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }
}
