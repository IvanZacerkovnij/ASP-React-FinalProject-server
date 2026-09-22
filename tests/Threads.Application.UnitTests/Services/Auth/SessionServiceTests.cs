using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Auth.Requests;
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
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsAuthResponse()
    {
        var user = CreateUser();
        var request = new LoginRequest
        {
            EmailOrUsername = "  USER@EXAMPLE.COM  ",
            Password = "Password123!"
        };
        var accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        _userRepository
            .GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher
            .VerifyPassword(request.Password, user.PasswordHash)
            .Returns(true);
        _tokenService.GenerateRefreshToken().Returns("refresh-token");
        _tokenService.GenerateAccessToken(user).Returns("access-token");
        _tokenService.GetAccessTokenExpiresAtUtc().Returns(accessTokenExpiresAt);

        var result = await _service.LoginAsync(request);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Username, result.Username);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.Equal(accessTokenExpiresAt, result.AccessTokenExpiresAt);
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(token =>
                token.UserId == user.Id &&
                !string.IsNullOrWhiteSpace(token.TokenHash) &&
                token.TokenHash != "refresh-token"),
            Arg.Any<CancellationToken>());
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
}
