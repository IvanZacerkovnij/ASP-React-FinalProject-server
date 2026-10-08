using System.Security.Cryptography;
using System.Text;
using NSubstitute;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Services.Auth;
using Threads.Application.UnitTests.Helpers;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Auth;

public class RefreshTokenManagerTests
{
    private readonly IRefreshTokenRepository _refreshTokenRepository =
        Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly RefreshTokenManager _manager;

    public RefreshTokenManagerTests()
    {
        _manager = new RefreshTokenManager(_refreshTokenRepository, _tokenService);
    }

    [Fact]
    public async Task GetStoredAsync_WhenTokenIsBlank_ThrowsRequestValidationException()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _manager.GetStoredAsync(" "));

        await _refreshTokenRepository.DidNotReceive().GetByTokenHashAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStoredAsync_HashesNormalizedTokenBeforeLookup()
    {
        const string refreshToken = "refresh-token";
        var storedToken = TestEntityFactory.CreateRefreshToken();
        _refreshTokenRepository
            .GetByTokenHashAsync(Hash(refreshToken), Arg.Any<CancellationToken>())
            .Returns(storedToken);

        var result = await _manager.GetStoredAsync($"  {refreshToken}  ");

        Assert.Same(storedToken, result);
        await _refreshTokenRepository.Received(1).GetByTokenHashAsync(
            Hash(refreshToken),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetActiveAsync_WhenTokenOrUserIsInactive_ReturnsNull(
        bool tokenIsActive,
        bool userIsActive)
    {
        const string rawToken = "refresh-token";
        var storedToken = TestEntityFactory.CreateRefreshToken(userIsActive);
        if (!tokenIsActive)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
        }

        _refreshTokenRepository
            .GetByTokenHashAsync(Hash(rawToken), Arg.Any<CancellationToken>())
            .Returns(storedToken);

        var result = await _manager.GetActiveAsync(rawToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveAsync_WhenTokenAndUserAreActive_ReturnsStoredToken()
    {
        const string rawToken = "refresh-token";
        var storedToken = TestEntityFactory.CreateRefreshToken();
        _refreshTokenRepository
            .GetByTokenHashAsync(Hash(rawToken), Arg.Any<CancellationToken>())
            .Returns(storedToken);

        var result = await _manager.GetActiveAsync(rawToken);

        Assert.Same(storedToken, result);
    }

    [Fact]
    public async Task IssueAsync_CreatesHashedTokenWithSevenDayLifetime()
    {
        var userId = Guid.NewGuid();
        var beforeIssue = DateTimeOffset.UtcNow;
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        RefreshToken? addedToken = null;
        _tokenService.GenerateRefreshToken().Returns("new-refresh-token");
        _refreshTokenRepository
            .AddAsync(
                Arg.Do<RefreshToken>(token => addedToken = token),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _manager.IssueAsync(userId, cancellationToken);

        Assert.Equal("new-refresh-token", result);
        Assert.NotNull(addedToken);
        Assert.Equal(userId, addedToken.UserId);
        Assert.Equal(Hash("new-refresh-token"), addedToken.TokenHash);
        Assert.InRange(
            addedToken.ExpiresAt,
            beforeIssue.AddDays(7),
            DateTimeOffset.UtcNow.AddDays(7));
        await _refreshTokenRepository.Received(1).AddAsync(addedToken, cancellationToken);
    }

    [Fact]
    public async Task RotateAsync_WhenRepositorySucceeds_ReturnsNewTokenAndPersistsHash()
    {
        var currentToken = TestEntityFactory.CreateRefreshToken();
        RefreshToken? replacementToken = null;
        _tokenService.GenerateRefreshToken().Returns("rotated-refresh-token");
        _refreshTokenRepository
            .TryRotateAsync(
                currentToken.Id,
                Arg.Do<RefreshToken>(token => replacementToken = token),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _manager.RotateAsync(currentToken);

        Assert.Equal("rotated-refresh-token", result);
        Assert.NotNull(replacementToken);
        Assert.Equal(currentToken.UserId, replacementToken.UserId);
        Assert.Equal(Hash("rotated-refresh-token"), replacementToken.TokenHash);
        Assert.True(replacementToken.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task RotateAsync_WhenRepositoryRejectsRotation_ReturnsNull()
    {
        var currentToken = TestEntityFactory.CreateRefreshToken();
        _tokenService.GenerateRefreshToken().Returns("rotated-refresh-token");
        _refreshTokenRepository
            .TryRotateAsync(
                currentToken.Id,
                Arg.Any<RefreshToken>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _manager.RotateAsync(currentToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_SetsRevocationTimeAndUpdatesToken()
    {
        var refreshToken = TestEntityFactory.CreateRefreshToken();
        var beforeRevoke = DateTimeOffset.UtcNow;

        await _manager.RevokeAsync(refreshToken);

        Assert.NotNull(refreshToken.RevokedAt);
        Assert.InRange(refreshToken.RevokedAt.Value, beforeRevoke, DateTimeOffset.UtcNow);
        await _refreshTokenRepository.Received(1).UpdateAsync(
            refreshToken,
            Arg.Any<CancellationToken>());
    }

    private static string Hash(string value)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
