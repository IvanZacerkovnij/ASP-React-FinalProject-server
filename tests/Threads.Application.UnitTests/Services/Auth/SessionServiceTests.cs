using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Auth;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Auth;

public class SessionServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository =
        Substitute.For<IRefreshTokenRepository>();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        var refreshTokenManager = new RefreshTokenManager(
            _refreshTokenRepository,
            _tokenService);

        _service = new SessionService(
            _userRepository,
            _passwordHasher,
            _tokenService,
            refreshTokenManager,
            Substitute.For<ILogger<SessionService>>());
    }

    [Fact]
    public async Task LoginAsync_WhenIdentityIsBlank_ThrowsRequestValidationException()
    {
        var request = new LoginRequest
        {
            EmailOrUsername = " ",
            Password = "Password123!"
        };

        await Assert.ThrowsAsync<RequestValidationException>(() => _service.LoginAsync(request));

        await _userRepository.DidNotReceive().GetByEmailAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().GetByUsernameAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsAuthResponse()
    {
        var user = CreateUser();
        var request = new LoginRequest
        {
            EmailOrUsername = "  USER@EXAMPLE.COM  ",
            Password = "Password123!"
        };
        var accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _userRepository
            .GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher
            .VerifyPassword(request.Password, user.PasswordHash)
            .Returns(true);
        _tokenService.GenerateRefreshToken().Returns("refresh-token");
        _tokenService.GenerateAccessToken(user).Returns("access-token");
        _tokenService.GetAccessTokenExpiresAtUtc().Returns(accessTokenExpiresAt);

        var result = await _service.LoginAsync(request, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Username, result.Username);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.Equal(accessTokenExpiresAt, result.AccessTokenExpiresAt);
        await _userRepository.Received(1).GetByEmailAsync(
            "user@example.com",
            cancellationToken);
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(token =>
                token.UserId == user.Id &&
                !string.IsNullOrWhiteSpace(token.TokenHash) &&
                token.TokenHash != "refresh-token"),
            cancellationToken);
    }

    [Fact]
    public async Task LoginAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        var request = new LoginRequest
        {
            EmailOrUsername = "  MissingUser  ",
            Password = "Password123!"
        };
        _userRepository
            .GetByUsernameAsync("missinguser", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _service.LoginAsync(request);

        Assert.Null(result);
        _passwordHasher.DidNotReceive().VerifyPassword(
            Arg.Any<string>(),
            Arg.Any<string>());
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsInvalid_ReturnsNull()
    {
        var user = CreateUser();
        var request = new LoginRequest
        {
            EmailOrUsername = user.Username,
            Password = "WrongPassword123!"
        };

        _userRepository
            .GetByUsernameAsync(user.Username, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher
            .VerifyPassword(request.Password, user.PasswordHash)
            .Returns(false);

        var result = await _service.LoginAsync(request);

        Assert.Null(result);
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
        _tokenService.DidNotReceive().GenerateRefreshToken();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task LoginAsync_WhenUserCannotLogin_ReturnsNull(
        bool isActive,
        bool isVerified)
    {
        var user = CreateUser(isActive, isVerified);
        var request = new LoginRequest
        {
            EmailOrUsername = user.Username,
            Password = "Password123!"
        };

        _userRepository
            .GetByUsernameAsync(user.Username, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.LoginAsync(request);

        Assert.Null(result);
        _passwordHasher.DidNotReceive().VerifyPassword(
            Arg.Any<string>(),
            Arg.Any<string>());
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenTokenIsInvalid_ReturnsNull()
    {
        var request = new RefreshTokenRequest { RefreshToken = "missing-refresh-token" };

        var result = await _service.RefreshTokenAsync(request);

        Assert.Null(result);
        await _refreshTokenRepository.DidNotReceive().TryRotateAsync(
            Arg.Any<Guid>(),
            Arg.Any<RefreshToken>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRotationFails_ReturnsNull()
    {
        const string rawToken = "current-refresh-token";
        var refreshToken = CreateRefreshToken();
        _refreshTokenRepository
            .GetByTokenHashAsync(Hash(rawToken), Arg.Any<CancellationToken>())
            .Returns(refreshToken);
        _tokenService.GenerateRefreshToken().Returns("new-refresh-token");
        _refreshTokenRepository
            .TryRotateAsync(
                refreshToken.Id,
                Arg.Any<RefreshToken>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = rawToken });

        Assert.Null(result);
        _tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenRotationSucceeds_ReturnsNewAuthResponse()
    {
        const string rawToken = "current-refresh-token";
        var refreshToken = CreateRefreshToken();
        var accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        _refreshTokenRepository
            .GetByTokenHashAsync(Hash(rawToken), Arg.Any<CancellationToken>())
            .Returns(refreshToken);
        _tokenService.GenerateRefreshToken().Returns("new-refresh-token");
        _refreshTokenRepository
            .TryRotateAsync(
                refreshToken.Id,
                Arg.Is<RefreshToken>(token =>
                    token.UserId == refreshToken.UserId &&
                    token.TokenHash == Hash("new-refresh-token")),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _tokenService.GenerateAccessToken(refreshToken.User).Returns("new-access-token");
        _tokenService.GetAccessTokenExpiresAtUtc().Returns(accessTokenExpiresAt);

        var result = await _service.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = rawToken });

        Assert.NotNull(result);
        Assert.Equal(refreshToken.UserId, result.UserId);
        Assert.Equal(refreshToken.User.Username, result.Username);
        Assert.Equal("new-access-token", result.AccessToken);
        Assert.Equal("new-refresh-token", result.RefreshToken);
        Assert.Equal(accessTokenExpiresAt, result.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenIsInactive_ReturnsFalse()
    {
        const string rawToken = "inactive-refresh-token";
        var refreshToken = CreateRefreshToken();
        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        _refreshTokenRepository
            .GetByTokenHashAsync(Hash(rawToken), Arg.Any<CancellationToken>())
            .Returns(refreshToken);

        var result = await _service.LogoutAsync(
            new LogoutRequest { RefreshToken = rawToken });

        Assert.False(result);
        await _refreshTokenRepository.DidNotReceive().UpdateAsync(
            Arg.Any<RefreshToken>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenDoesNotExist_ReturnsFalse()
    {
        const string rawToken = "missing-refresh-token";

        var result = await _service.LogoutAsync(
            new LogoutRequest { RefreshToken = rawToken });

        Assert.False(result);
        await _refreshTokenRepository.Received(1).GetByTokenHashAsync(
            Hash(rawToken),
            Arg.Any<CancellationToken>());
        await _refreshTokenRepository.DidNotReceive().UpdateAsync(
            Arg.Any<RefreshToken>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenIsActive_RevokesTokenAndReturnsTrue()
    {
        const string rawToken = "active-refresh-token";
        var refreshToken = CreateRefreshToken();
        _refreshTokenRepository
            .GetByTokenHashAsync(Hash(rawToken), Arg.Any<CancellationToken>())
            .Returns(refreshToken);

        var result = await _service.LogoutAsync(
            new LogoutRequest { RefreshToken = rawToken });

        Assert.True(result);
        Assert.NotNull(refreshToken.RevokedAt);
        await _refreshTokenRepository.Received(1).UpdateAsync(
            refreshToken,
            Arg.Any<CancellationToken>());
    }

    private static User CreateUser(bool isActive = true, bool isVerified = true)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Username = "testuser",
            PasswordHash = "stored-password-hash",
            IsActive = isActive,
            IsVerified = isVerified
        };
    }

    private static RefreshToken CreateRefreshToken()
    {
        var user = CreateUser();
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "stored-token-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        };
    }

    private static string Hash(string value)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
