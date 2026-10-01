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

public class PasswordRecoveryServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository =
        Substitute.For<IRefreshTokenRepository>();
    private readonly IAuthTransaction _authTransaction = Substitute.For<IAuthTransaction>();
    private readonly IAuthEmailService _authEmailService = Substitute.For<IAuthEmailService>();
    private readonly IAuthCodeHasher _authCodeHasher = Substitute.For<IAuthCodeHasher>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly PasswordRecoveryService _service;

    public PasswordRecoveryServiceTests()
    {
        var passwordChangeService = new PasswordChangeService(
            _userRepository,
            _refreshTokenRepository,
            _authTransaction,
            _authEmailService,
            _authCodeHasher,
            _passwordHasher,
            Substitute.For<ILogger<PasswordChangeService>>());

        _service = new PasswordRecoveryService(
            _userRepository,
            _authEmailService,
            _authCodeHasher,
            _passwordHasher,
            passwordChangeService,
            Substitute.For<ILogger<PasswordRecoveryService>>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserDoesNotExist_DoesNotRevealOrPersistAnything()
    {
        var request = new ForgotPasswordRequest { Email = "  MISSING@EXAMPLE.COM  " };

        await _service.ForgotPasswordAsync(request);

        await _userRepository.Received(1).GetByEmailAsync(
            "missing@example.com",
            Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
        await _authEmailService.DidNotReceive().SendPasswordResetCodeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserIsInactive_DoesNotSendCode()
    {
        var user = CreateUser(isActive: false);
        _userRepository
            .GetByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);

        await _service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });

        await _userRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
        await _authEmailService.DidNotReceive().SendPasswordResetCodeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenUserIsActive_SavesHashedCodeAndSendsPlainCode()
    {
        var user = CreateUser();
        user.PendingPasswordHash = "stale-pending-hash";
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _userRepository
            .GetByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _authCodeHasher
            .Hash(Arg.Any<string>(), $"password-reset:{user.Id:N}")
            .Returns("reset-code-hash");

        await _service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = user.Email },
            cancellationToken);

        Assert.Null(user.PendingPasswordHash);
        Assert.Equal("reset-code-hash", user.PasswordResetCodeHash);
        Assert.True(user.PasswordResetCodeExpiresAt > DateTimeOffset.UtcNow);
        Assert.NotNull(user.UpdatedAt);
        await _userRepository.Received(1).UpdateAsync(user, cancellationToken);
        await _authEmailService.Received(1).SendPasswordResetCodeAsync(
            user.Email,
            Arg.Is<string>(code => code.Length == 6 && code.All(char.IsDigit)),
            cancellationToken);
    }

    [Fact]
    public async Task VerifyResetCodeAsync_WhenCodeIsValid_ReturnsTrue()
    {
        var user = CreateUserWithResetCode();
        _userRepository
            .GetByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _authCodeHasher
            .Verify("123456", $"password-reset:{user.Id:N}", user.PasswordResetCodeHash!)
            .Returns(true);

        var result = await _service.VerifyResetCodeAsync(
            new VerifyResetCodeRequest { Email = user.Email, Code = "123456" });

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyResetCodeAsync_WhenUserDoesNotExist_ReturnsFalse()
    {
        var request = new VerifyResetCodeRequest
        {
            Email = "  MISSING@EXAMPLE.COM  ",
            Code = "123456"
        };

        var result = await _service.VerifyResetCodeAsync(request);

        Assert.False(result);
        await _userRepository.Received(1).GetByEmailAsync(
            "missing@example.com",
            Arg.Any<CancellationToken>());
        _authCodeHasher.DidNotReceive().Verify(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task VerifyResetCodeAsync_WhenCodeIsExpired_ReturnsFalseWithoutHashVerification()
    {
        var user = CreateUserWithResetCode();
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _userRepository
            .GetByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.VerifyResetCodeAsync(
            new VerifyResetCodeRequest { Email = user.Email, Code = "123456" });

        Assert.False(result);
        _authCodeHasher.DidNotReceive().Verify(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task VerifyResetCodeAsync_WhenCodeFormatIsInvalid_ThrowsRequestValidationException()
    {
        var user = CreateUserWithResetCode();
        _userRepository
            .GetByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.VerifyResetCodeAsync(
                new VerifyResetCodeRequest { Email = user.Email, Code = "12345x" }));

        _authCodeHasher.DidNotReceive().Verify(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenCodeIsInvalid_ReturnsFalseWithoutChangingPassword()
    {
        var user = CreateUserWithResetCode();
        var request = new ResetPasswordRequest
        {
            Email = user.Email,
            Code = "123456",
            NewPassword = "NewPassword123!"
        };
        _userRepository
            .GetByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.ResetPasswordAsync(request);

        Assert.False(result);
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _authTransaction.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WhenCodeIsValid_ChangesPasswordAndRevokesSessions()
    {
        var user = CreateUserWithResetCode();
        var request = new ResetPasswordRequest
        {
            Email = "  USER@EXAMPLE.COM  ",
            Code = "123456",
            NewPassword = "NewPassword123!"
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        ConfigureAuthTransaction();
        _userRepository
            .GetByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _authCodeHasher
            .Verify(request.Code, $"password-reset:{user.Id:N}", user.PasswordResetCodeHash!)
            .Returns(true);
        _passwordHasher.HashPassword(request.NewPassword).Returns("new-password-hash");

        var result = await _service.ResetPasswordAsync(request, cancellationToken);

        Assert.True(result);
        Assert.Equal("new-password-hash", user.PasswordHash);
        Assert.Null(user.PasswordResetCodeHash);
        Assert.Null(user.PasswordResetCodeExpiresAt);
        await _authTransaction.Received(1).ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            cancellationToken);
        await _userRepository.Received(1).UpdateAsync(user, cancellationToken);
        await _refreshTokenRepository.Received(1).RevokeAllByUserIdAsync(
            user.Id,
            Arg.Any<DateTimeOffset>(),
            cancellationToken);
    }

    private void ConfigureAuthTransaction()
    {
        _authTransaction
            .ExecuteAsync(
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var operation = callInfo.ArgAt<Func<CancellationToken, Task>>(0);
                var cancellationToken = callInfo.ArgAt<CancellationToken>(1);
                return operation(cancellationToken);
            });
    }

    private static User CreateUser(bool isActive = true)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Username = "testuser",
            PasswordHash = "current-password-hash",
            IsActive = isActive
        };
    }

    private static User CreateUserWithResetCode()
    {
        var user = CreateUser();
        user.PasswordResetCodeHash = "reset-code-hash";
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        return user;
    }
}
