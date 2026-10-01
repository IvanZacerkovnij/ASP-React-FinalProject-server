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

public class RegistrationServiceTests
{
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();

    private readonly IPendingRegistrationRepository pendingRegistrationRepository =
        Substitute.For<IPendingRegistrationRepository>();

    private readonly IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuthEmailService authEmailService = Substitute.For<IAuthEmailService>();
    private readonly IAuthTransaction authTransaction = Substitute.For<IAuthTransaction>();

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
            authTransaction,
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

    [Fact]
    public async Task RegisterAsync_WhenEmailAndUsernameBelongToDifferentPendingRegistrations_ThrowsConflictException()
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Username = "username",
            Password = "Password123!"
        };

        var pendingRegistrationByEmail = new PendingRegistration
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Username = "another-username",
            PasswordHash = "stored-password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        var pendingRegistrationByUsername = new PendingRegistration
        {
            Id = Guid.NewGuid(),
            Email = "another@example.com",
            Username = request.Username,
            PasswordHash = "stored-password-hash",
            VerificationCode = "654321",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(pendingRegistrationByEmail);
        pendingRegistrationRepository
            .GetByUsernameAsync(request.Username, Arg.Any<CancellationToken>())
            .Returns(pendingRegistrationByUsername);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => registrationService.RegisterAsync(request));

        Assert.Equal(
            "Email or username is already used in another pending registration.",
            exception.Message);
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
    public async Task RegisterAsync_WhenPendingRegistrationIsExpired_DeletesItAndAddsNewRegistration()
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Username = "username",
            Password = "Password123!"
        };
        var expiredPendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = "old-username",
            PasswordHash = "old-password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        PendingRegistration? addedRegistration = null;

        passwordHasher.HashPassword(request.Password).Returns("new-password-hash");
        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(expiredPendingRegistration);
        pendingRegistrationRepository
            .AddAsync(
                Arg.Do<PendingRegistration>(registration => addedRegistration = registration),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await registrationService.RegisterAsync(request);

        Assert.NotNull(addedRegistration);
        Assert.NotSame(expiredPendingRegistration, addedRegistration);
        Assert.Equal("user@example.com", addedRegistration.Email);
        Assert.Equal("username", addedRegistration.Username);
        Assert.Equal("new-password-hash", addedRegistration.PasswordHash);
        Assert.Matches("^[0-9]{6}$", addedRegistration.VerificationCode);
        Assert.True(addedRegistration.VerificationCodeExpiresAt > DateTimeOffset.UtcNow);
        await pendingRegistrationRepository.Received(1).DeleteAsync(
            expiredPendingRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.Received(1).AddAsync(
            addedRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().UpdateAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.Received(1).SendEmailVerificationCodeAsync(
            "user@example.com",
            addedRegistration.VerificationCode,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WhenUsernameIsGuid_ThrowsRequestValidationException()
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Username = Guid.NewGuid().ToString(),
            Password = "Password123!"
        };

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => registrationService.RegisterAsync(request));

        Assert.Equal("Username must not be a GUID.", exception.Message);
        await userRepository.DidNotReceive().GetByEmailAsync(
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
    public async Task RegisterAsync_WhenPendingRegistrationExistsByUsername_UpdatesRegistrationInsteadOfAdding()
    {
        var request = new RegisterRequest
        {
            Email = "new@example.com",
            Username = "username",
            Password = "Password123!",
            DisplayName = "New Display Name"
        };
        var existingPendingRegistration = new PendingRegistration
        {
            Email = "old@example.com",
            Username = request.Username,
            PasswordHash = "old-password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        PendingRegistration? updatedRegistration = null;

        passwordHasher.HashPassword(request.Password).Returns("new-password-hash");
        pendingRegistrationRepository
            .GetByUsernameAsync(request.Username, Arg.Any<CancellationToken>())
            .Returns(existingPendingRegistration);
        pendingRegistrationRepository
            .UpdateAsync(
                Arg.Do<PendingRegistration>(registration => updatedRegistration = registration),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await registrationService.RegisterAsync(request);

        Assert.NotNull(updatedRegistration);
        Assert.Same(existingPendingRegistration, updatedRegistration);
        Assert.Equal("new@example.com", updatedRegistration.Email);
        Assert.Equal("username", updatedRegistration.Username);
        Assert.Equal("new-password-hash", updatedRegistration.PasswordHash);
        Assert.Equal("New Display Name", updatedRegistration.DisplayName);
        Assert.Matches("^[0-9]{6}$", updatedRegistration.VerificationCode);
        await pendingRegistrationRepository.Received(1).UpdateAsync(
            updatedRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().AddAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.Received(1).SendEmailVerificationCodeAsync(
            "new@example.com",
            updatedRegistration.VerificationCode,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAndUsernameBelongToSamePendingRegistration_UpdatesIt()
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Username = "username",
            Password = "Password123!"
        };
        var existingPendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = request.Username,
            PasswordHash = "old-password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        passwordHasher.HashPassword(request.Password).Returns("new-password-hash");
        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(existingPendingRegistration);
        pendingRegistrationRepository
            .GetByUsernameAsync(request.Username, Arg.Any<CancellationToken>())
            .Returns(existingPendingRegistration);

        await registrationService.RegisterAsync(request);

        await pendingRegistrationRepository.Received(1).UpdateAsync(
            existingPendingRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().AddAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.Received(1).SendEmailVerificationCodeAsync(
            request.Email,
            existingPendingRegistration.VerificationCode,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenPendingRegistrationDoesNotExist_ReturnsNull()
    {
        var request = new VerifyEmailRequest
        {
            Email = "  USER@EXAMPLE.COM  ",
            Code = "123456"
        };

        var response = await registrationService.VerifyEmailAsync(request);

        Assert.Null(response);
        await pendingRegistrationRepository.Received(1).GetByEmailAsync(
            "user@example.com",
            Arg.Any<CancellationToken>());
        await authTransaction.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task<AuthResponse>>>(),
            Arg.Any<CancellationToken>());
        await userRepository.DidNotReceive().AddAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenCodeIsInvalid_ReturnsNull()
    {
        var request = new VerifyEmailRequest
        {
            Email = "user@example.com",
            Code = "654321"
        };
        var pendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = "username",
            PasswordHash = "password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(pendingRegistration);

        var response = await registrationService.VerifyEmailAsync(request);

        Assert.Null(response);
        await authTransaction.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task<AuthResponse>>>(),
            Arg.Any<CancellationToken>());
        await userRepository.DidNotReceive().AddAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().DeleteAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenPendingRegistrationIsExpired_DeletesItAndReturnsNull()
    {
        var request = new VerifyEmailRequest
        {
            Email = "user@example.com",
            Code = "123456"
        };
        var expiredPendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = "username",
            PasswordHash = "password-hash",
            VerificationCode = request.Code,
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(expiredPendingRegistration);

        var response = await registrationService.VerifyEmailAsync(request);

        Assert.Null(response);
        await pendingRegistrationRepository.Received(1).DeleteAsync(
            expiredPendingRegistration,
            Arg.Any<CancellationToken>());
        await authTransaction.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task<AuthResponse>>>(),
            Arg.Any<CancellationToken>());
        await userRepository.DidNotReceive().AddAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenCodeIsValid_CreatesVerifiedUserAndReturnsAuthResponse()
    {
        var request = new VerifyEmailRequest
        {
            Email = "user@example.com",
            Code = "123456"
        };
        var pendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = "username",
            PasswordHash = "password-hash",
            DisplayName = "Display Name",
            VerificationCode = request.Code,
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        var accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        User? addedUser = null;

        ConfigureAuthTransaction();
        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(pendingRegistration);
        userRepository
            .AddAsync(
                Arg.Do<User>(user => addedUser = user),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        tokenService.GenerateRefreshToken().Returns("refresh-token");
        tokenService.GenerateAccessToken(Arg.Any<User>()).Returns("access-token");
        tokenService.GetAccessTokenExpiresAtUtc().Returns(accessTokenExpiresAt);

        var response = await registrationService.VerifyEmailAsync(request);

        Assert.NotNull(response);
        Assert.NotNull(addedUser);
        Assert.Equal(addedUser.Id, response.UserId);
        Assert.Equal("username", response.Username);
        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("refresh-token", response.RefreshToken);
        Assert.Equal(accessTokenExpiresAt, response.AccessTokenExpiresAt);
        Assert.Equal(pendingRegistration.Email, addedUser.Email);
        Assert.Equal(pendingRegistration.Username, addedUser.Username);
        Assert.Equal(pendingRegistration.PasswordHash, addedUser.PasswordHash);
        Assert.Equal(pendingRegistration.DisplayName, addedUser.DisplayName);
        Assert.True(addedUser.IsVerified);
        Assert.True(addedUser.IsActive);
        await pendingRegistrationRepository.Received(1).DeleteAsync(
            pendingRegistration,
            Arg.Any<CancellationToken>());
        await refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(token => token.UserId == addedUser.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_WhenUserAlreadyExists_ThrowsConflictException()
    {
        var request = new VerifyEmailRequest
        {
            Email = "user@example.com",
            Code = "123456"
        };
        var pendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = "username",
            PasswordHash = "password-hash",
            VerificationCode = request.Code,
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        var existingUser = new User
        {
            Email = request.Email,
            Username = "another-username",
            PasswordHash = "password-hash"
        };

        ConfigureAuthTransaction();
        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(pendingRegistration);
        userRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => registrationService.VerifyEmailAsync(request));

        Assert.Equal("User with this email already exists.", exception.Message);
        await userRepository.DidNotReceive().AddAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().DeleteAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        tokenService.DidNotReceive().GenerateAccessToken(Arg.Any<User>());
        tokenService.DidNotReceive().GenerateRefreshToken();
    }

    [Fact]
    public async Task ResendVerificationCodeAsync_WhenPendingRegistrationDoesNotExist_ReturnsFalse()
    {
        var request = new ResendVerificationCodeRequest
        {
            Email = "  USER@EXAMPLE.COM  "
        };

        var result = await registrationService.ResendVerificationCodeAsync(request);

        Assert.False(result);
        await pendingRegistrationRepository.Received(1).GetByEmailAsync(
            "user@example.com",
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().UpdateAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.DidNotReceive().SendEmailVerificationCodeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationCodeAsync_WhenPendingRegistrationIsExpired_DeletesItAndReturnsFalse()
    {
        var request = new ResendVerificationCodeRequest
        {
            Email = "user@example.com"
        };
        var expiredPendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = "username",
            PasswordHash = "password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(expiredPendingRegistration);

        var result = await registrationService.ResendVerificationCodeAsync(request);

        Assert.False(result);
        await pendingRegistrationRepository.Received(1).DeleteAsync(
            expiredPendingRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().UpdateAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.DidNotReceive().SendEmailVerificationCodeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationCodeAsync_WhenPendingRegistrationIsActive_UpdatesAndSendsCode()
    {
        var request = new ResendVerificationCodeRequest
        {
            Email = "user@example.com"
        };
        var pendingRegistration = new PendingRegistration
        {
            Email = request.Email,
            Username = "username",
            PasswordHash = "password-hash",
            VerificationCode = "123456",
            VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1)
        };
        var previousExpiration = pendingRegistration.VerificationCodeExpiresAt;

        pendingRegistrationRepository
            .GetByEmailAsync(request.Email, Arg.Any<CancellationToken>())
            .Returns(pendingRegistration);

        var result = await registrationService.ResendVerificationCodeAsync(request);

        Assert.True(result);
        Assert.Matches("^[0-9]{6}$", pendingRegistration.VerificationCode);
        Assert.True(pendingRegistration.VerificationCodeExpiresAt > previousExpiration);
        Assert.NotNull(pendingRegistration.UpdatedAt);
        await pendingRegistrationRepository.Received(1).UpdateAsync(
            pendingRegistration,
            Arg.Any<CancellationToken>());
        await pendingRegistrationRepository.DidNotReceive().AddAsync(
            Arg.Any<PendingRegistration>(),
            Arg.Any<CancellationToken>());
        await authEmailService.Received(1).SendEmailVerificationCodeAsync(
            request.Email,
            pendingRegistration.VerificationCode,
            Arg.Any<CancellationToken>());
    }

    private void ConfigureAuthTransaction()
    {
        authTransaction
            .ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task<AuthResponse>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var operation = callInfo.ArgAt<Func<CancellationToken, Task<AuthResponse>>>(0);
                var cancellationToken = callInfo.ArgAt<CancellationToken>(1);
                return operation(cancellationToken);
            });
    }
        
}
