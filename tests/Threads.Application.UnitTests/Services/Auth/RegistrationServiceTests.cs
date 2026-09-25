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

public class RegistrationServiceTests
{
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();

    private readonly IPendingRegistrationRepository pendingRegistrationRepository =
        Substitute.For<IPendingRegistrationRepository>();

    private readonly IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuthEmailService authEmailService = Substitute.For<IAuthEmailService>();

    private readonly RefreshTokenManager refreshTokenManager;
    private readonly SessionService sessionService;
    private readonly RegistrationService registrationService;

    public RegistrationServiceTests()
    {
        refreshTokenManager = new RefreshTokenManager(
            refreshTokenRepository,
            tokenService);

        sessionService = new SessionService(
            userRepository,
            passwordHasher,
            tokenService,
            refreshTokenManager,
            Substitute.For<ILogger<SessionService>>());

        registrationService = new RegistrationService(
            userRepository,
            pendingRegistrationRepository,
            Substitute.For<IAuthTransaction>(),
            authEmailService,
            passwordHasher,
            sessionService,
            Substitute.For<ILogger<RegistrationService>>());
    }

    [Fact]
    public async Task RegisterAsync_WhenRequestIsValid_AddsPendingRegistrationAndSendsVerificationCode()
    {
        var request = new RegisterRequest
        {
            Email = "  USER@EXAMPLE.COM  ",
            Username = "  TestUser  ",
            Password = "Password123!",
            DisplayName = "  Test User  "
        };
        PendingRegistration? savedRegistration = null;
        passwordHasher.HashPassword(request.Password).Returns("hashed-password");
        pendingRegistrationRepository
            .AddAsync(
                Arg.Do<PendingRegistration>(registration => savedRegistration = registration),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await registrationService.RegisterAsync(request);

        Assert.NotNull(savedRegistration);
        Assert.Equal("user@example.com", savedRegistration.Email);
        Assert.Equal("testuser", savedRegistration.Username);
        Assert.Equal("hashed-password", savedRegistration.PasswordHash);
        Assert.Equal("Test User", savedRegistration.DisplayName);
        Assert.Matches("^[0-9]{6}$", savedRegistration.VerificationCode);
        Assert.True(savedRegistration.VerificationCodeExpiresAt > DateTimeOffset.UtcNow);
        await pendingRegistrationRepository.Received(1).AddAsync(
            savedRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().UpdateAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.Received(1).SendEmailVerificationCodeAsync(
            "user@example.com",
            savedRegistration.VerificationCode,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsConflictException()
    {
        var request = new RegisterRequest
        {
            Email = "  USER@EXAMPLE.COM  ",
            Username = "NewUser",
            Password = "Password123!"
        };

        var existingUser = new User
        {
            Email = "user@example.com",
            Username = "existinguser",
            PasswordHash = "stored-password-hash"
        };

        userRepository
            .GetByEmailAsync(
                "user@example.com",
                Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => registrationService.RegisterAsync(request));

        Assert.Equal("User with this email already exists.", exception.Message);

        await userRepository.DidNotReceive().GetByUsernameAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await pendingRegistrationRepository.DidNotReceive().AddAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());

        await pendingRegistrationRepository.DidNotReceive().UpdateAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());

        passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());

        await authEmailService.DidNotReceive().SendEmailVerificationCodeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WhenPendingRegistrationExists_UpdatesRegistrationInsteadOfAdding()
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Username = "NewUser",
            Password = "Password123!"
        };

        var existingPendingRegistration = new PendingRegistration()
        {
            Username = "pending-Username",
            Email = "user@example.com",
            PasswordHash = "stored-password-hash",
            VerificationCode = "verification-code",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        PendingRegistration? updatedRegistration = null;
        passwordHasher.HashPassword(request.Password).Returns("new-password-hash");
        pendingRegistrationRepository.GetByEmailAsync(request.Email,
                Arg.Any<CancellationToken>())
            .Returns(existingPendingRegistration);

        pendingRegistrationRepository
            .UpdateAsync(
                Arg.Do<PendingRegistration>(registration => updatedRegistration = registration),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await registrationService.RegisterAsync(request);

        Assert.NotNull(updatedRegistration);
        Assert.Same(existingPendingRegistration, updatedRegistration);
        Assert.Equal("user@example.com", updatedRegistration.Email);
        Assert.Equal("newuser", updatedRegistration.Username);
        Assert.Equal("new-password-hash", updatedRegistration.PasswordHash);
        Assert.Matches("^[0-9]{6}$", updatedRegistration.VerificationCode);
        Assert.True(updatedRegistration.VerificationCodeExpiresAt > DateTimeOffset.UtcNow);
        
        await pendingRegistrationRepository.Received(1).UpdateAsync(
            updatedRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().AddAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.Received(1).SendEmailVerificationCodeAsync(
            "user@example.com",
            updatedRegistration.VerificationCode,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WhenUsernameAlreadyExists_ThrowsConflictException()
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Username = "username",
            Password = "Password123!"
        };

        var existingUser = new User
        {
            Username = "username"
        };
        
        userRepository.GetByUsernameAsync(
            request.Username,
            Arg.Any<CancellationToken>())
            .Returns(existingUser);
        
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => registrationService.RegisterAsync(request));
        
        Assert.Equal("User with this username already exists.", exception.Message);
        
        await userRepository.Received(1).GetByUsernameAsync(
            "username",
            Arg.Any<CancellationToken>());
        
        await pendingRegistrationRepository.DidNotReceive().AddAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());

        await pendingRegistrationRepository.DidNotReceive().UpdateAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());

        passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());

        await authEmailService.DidNotReceive().SendEmailVerificationCodeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
