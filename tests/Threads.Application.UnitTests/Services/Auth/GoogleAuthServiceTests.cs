using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Auth;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Auth;

public sealed class GoogleAuthServiceTests
{
    private readonly IGoogleTokenValidator validator = Substitute.For<IGoogleTokenValidator>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly GoogleAuthService service;
    private readonly GoogleLoginRequest request = new() { IdToken = "google-id-token" };

    public GoogleAuthServiceTests()
    {
        var tokens = Substitute.For<ITokenService>();
        tokens.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");
        tokens.GenerateRefreshToken().Returns("refresh-token");
        var transaction = Substitute.For<IAuthTransaction>();
        transaction.ExecuteAsync(Arg.Any<Func<CancellationToken, Task<AuthResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<AuthResponse>>>()(call.Arg<CancellationToken>()));
        var sessions = new SessionService(users, Substitute.For<IPasswordHasher>(), tokens,
            new RefreshTokenManager(refreshTokens, tokens), Substitute.For<ILogger<SessionService>>());
        service = new GoogleAuthService(validator, users, transaction, sessions);
        validator.ValidateAsync(request.IdToken, Arg.Any<CancellationToken>())
            .Returns(new GoogleIdentity("subject", "USER@gmail.com", "Test User", true));
    }

    [Fact]
    public async Task LoginAsync_NewIdentity_CreatesVerifiedUserWithoutPasswordAndIssuesSession()
    {
        User? saved = null;
        users.AddAsync(Arg.Do<User>(user => saved = user), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        using var source = new CancellationTokenSource();

        var response = await service.LoginAsync(request, source.Token);

        Assert.NotNull(saved);
        Assert.NotNull(response);
        Assert.Equal("user@gmail.com", saved.Email);
        Assert.Equal("subject", saved.GoogleSubject);
        Assert.Null(saved.PasswordHash);
        Assert.True(saved.IsVerified);
        Assert.Equal(saved.Id, response.UserId);
        Assert.Equal("access-token", response.AccessToken);
        await validator.Received(1).ValidateAsync(request.IdToken, source.Token);
        await refreshTokens.Received(1).AddAsync(Arg.Is<RefreshToken>(token => token.UserId == saved.Id), source.Token);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task LoginAsync_UnavailableLinkedUser_DoesNotCreateUserOrIssueSession(
        bool active, bool verified, bool deleted)
    {
        users.GetByGoogleSubjectAsync("subject", Arg.Any<CancellationToken>()).Returns(new User
        {
            IsActive = active, IsVerified = verified, DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        });

        Assert.Null(await service.LoginAsync(request));
        await users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await refreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ExistingEmail_RequiresExplicitLinking()
    {
        users.GetByEmailAsync("user@gmail.com", Arg.Any<CancellationToken>()).Returns(new User());

        await Assert.ThrowsAsync<ConflictException>(() => service.LoginAsync(request));
        await users.DidNotReceive().TryLinkGoogleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await refreshTokens.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ThirdPartyEmail_RequiresLocalVerification()
    {
        validator.ValidateAsync(request.IdToken, Arg.Any<CancellationToken>())
            .Returns(new GoogleIdentity("subject", "user@example.com", null, false));

        await Assert.ThrowsAsync<RequestValidationException>(() => service.LoginAsync(request));
        await users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_InvalidToken_DoesNotQueryUsers()
    {
        validator.ValidateAsync(request.IdToken, Arg.Any<CancellationToken>()).Returns((GoogleIdentity?)null);

        Assert.Null(await service.LoginAsync(request));
        await users.DidNotReceive().GetByGoogleSubjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkAsync_AccountAlreadyLinked_DoesNotReplaceLink()
    {
        var user = new User { IsVerified = true };
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        users.TryLinkGoogleAsync(user.Id, "subject", Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ConflictException>(() => service.LinkAsync(user.Id, request));
    }
}
