using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Auth;
using Threads.Application.UnitTests.Helpers;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Auth;

public class PasswordChangeServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository =
        Substitute.For<IRefreshTokenRepository>();
    private readonly IAuthTransaction _authTransaction = Substitute.For<IAuthTransaction>();
    private readonly IAuthEmailService _authEmailService = Substitute.For<IAuthEmailService>();
    private readonly IAuthCodeHasher _authCodeHasher = Substitute.For<IAuthCodeHasher>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly PasswordChangeService _service;

    public PasswordChangeServiceTests()
    {
        _service = new PasswordChangeService(
            _userRepository,
            _refreshTokenRepository,
            _authTransaction,
            _authEmailService,
            _authCodeHasher,
            _passwordHasher,
            Substitute.For<ILogger<PasswordChangeService>>());
    }

    [Fact]
    public async Task StartPasswordChangeAsync_WhenPasswordIsBlank_ThrowsRequestValidationException()
    {
        var request = new StartPasswordChangeRequest
        {
            CurrentPassword = " ",
            NewPassword = "NewPassword123!"
        };

        await Assert.ThrowsAsync<RequestValidationException>(
            () => _service.StartPasswordChangeAsync(Guid.NewGuid(), request));

        await _userRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(65)]
    public async Task StartPasswordChangeAsync_WhenNewPasswordLengthIsInvalid_ThrowsRequestValidationException(
        int passwordLength)
    {
        var request = new StartPasswordChangeRequest
        {
            CurrentPassword = "CurrentPassword123!",
            NewPassword = new string('p', passwordLength)
        };

        await Assert.ThrowsAsync<RequestValidationException>(
            () => _service.StartPasswordChangeAsync(Guid.NewGuid(), request));

        await _userRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    public async Task StartPasswordChangeAsync_WhenNewPasswordLengthIsAtBoundary_AcceptsPassword(
        int passwordLength)
    {
        var userId = Guid.NewGuid();
        var request = new StartPasswordChangeRequest
        {
            CurrentPassword = "CurrentPassword123!",
            NewPassword = new string('p', passwordLength)
        };

        var result = await _service.StartPasswordChangeAsync(userId, request);

        Assert.Equal(ChangePasswordStatus.UserNotFound, result.Status);
        await _userRepository.Received(1).GetByIdAsync(
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartPasswordChangeAsync_WhenUserDoesNotExist_ReturnsUserNotFound()
    {
        var userId = Guid.NewGuid();
        var request = CreateStartRequest();

        var result = await _service.StartPasswordChangeAsync(userId, request);

        Assert.Equal(ChangePasswordStatus.UserNotFound, result.Status);
        _passwordHasher.DidNotReceive().VerifyPassword(Arg.Any<string>(), Arg.Any<string>());
        await _userRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartPasswordChangeAsync_WhenUserIsInactive_ReturnsUserNotFound()
    {
        var user = TestEntityFactory.CreateUser(isActive: false);
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.StartPasswordChangeAsync(user.Id, CreateStartRequest());

        Assert.Equal(ChangePasswordStatus.UserNotFound, result.Status);
        _passwordHasher.DidNotReceive().VerifyPassword(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StartPasswordChangeAsync_WhenCurrentPasswordIsInvalid_ReturnsInvalidCurrentPassword()
    {
        var user = TestEntityFactory.CreateUser();
        var request = CreateStartRequest();
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher
            .VerifyPassword(request.CurrentPassword, user.PasswordHash)
            .Returns(false);

        var result = await _service.StartPasswordChangeAsync(user.Id, request);

        Assert.Equal(ChangePasswordStatus.InvalidCurrentPassword, result.Status);
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _authEmailService.DidNotReceive().SendPasswordChangeCodeAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartPasswordChangeAsync_WhenNewPasswordMatchesCurrent_ReturnsInvalidNewPassword()
    {
        var user = TestEntityFactory.CreateUser();
        var request = CreateStartRequest();
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher
            .VerifyPassword(Arg.Any<string>(), user.PasswordHash)
            .Returns(true);

        var result = await _service.StartPasswordChangeAsync(user.Id, request);

        Assert.Equal(ChangePasswordStatus.InvalidNewPassword, result.Status);
        _passwordHasher.DidNotReceive().HashPassword(Arg.Any<string>());
        await _userRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartPasswordChangeAsync_WhenRequestIsValid_SavesPendingHashAndSendsCode()
    {
        var user = TestEntityFactory.CreateUser();
        var request = CreateStartRequest();
        var previousUpdatedAt = user.UpdatedAt;
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher
            .VerifyPassword(request.CurrentPassword, user.PasswordHash)
            .Returns(true);
        _passwordHasher
            .VerifyPassword(request.NewPassword, user.PasswordHash)
            .Returns(false);
        _passwordHasher.HashPassword(request.NewPassword).Returns("new-password-hash");
        _authCodeHasher
            .Hash(Arg.Any<string>(), $"password-change:{user.Id:N}")
            .Returns("confirmation-code-hash");

        var result = await _service.StartPasswordChangeAsync(user.Id, request, cancellationToken);

        Assert.Equal(ChangePasswordStatus.ConfirmationCodeSent, result.Status);
        Assert.Equal("new-password-hash", user.PendingPasswordHash);
        Assert.Equal("confirmation-code-hash", user.PasswordResetCodeHash);
        Assert.True(user.PasswordResetCodeExpiresAt > DateTimeOffset.UtcNow);
        Assert.NotEqual(previousUpdatedAt, user.UpdatedAt);
        await _userRepository.Received(1).UpdateAsync(user, cancellationToken);
        await _authEmailService.Received(1).SendPasswordChangeCodeAsync(
            user.Email,
            Arg.Is<string>(code => code.Length == 6 && code.All(char.IsDigit)),
            cancellationToken);
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WhenNoChangeIsPending_ReturnsNoPendingPasswordChange()
    {
        var user = TestEntityFactory.CreateUser();
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.ConfirmPasswordChangeAsync(
            user.Id,
            new ConfirmPasswordChangeRequest { Code = "123456" });

        Assert.Equal(ChangePasswordStatus.NoPendingPasswordChange, result.Status);
        _authCodeHasher.DidNotReceive().Verify(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WhenUserIsInactive_ReturnsUserNotFound()
    {
        var user = TestEntityFactory.CreateUser(isActive: false);
        user.PendingPasswordHash = "new-password-hash";
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.ConfirmPasswordChangeAsync(
            user.Id,
            new ConfirmPasswordChangeRequest { Code = "123456" });

        Assert.Equal(ChangePasswordStatus.UserNotFound, result.Status);
        _authCodeHasher.DidNotReceive().Verify(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WhenCodeIsInvalid_ReturnsInvalidConfirmationCode()
    {
        var user = TestEntityFactory.CreateUser();
        user.PendingPasswordHash = "new-password-hash";
        user.PasswordResetCodeHash = "confirmation-code-hash";
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.ConfirmPasswordChangeAsync(
            user.Id,
            new ConfirmPasswordChangeRequest { Code = "123456" });

        Assert.Equal(ChangePasswordStatus.InvalidConfirmationCode, result.Status);
        await _authTransaction.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WhenCodeIsExpired_ReturnsInvalidConfirmationCode()
    {
        var user = TestEntityFactory.CreateUser();
        user.PendingPasswordHash = "new-password-hash";
        user.PasswordResetCodeHash = "confirmation-code-hash";
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _service.ConfirmPasswordChangeAsync(
            user.Id,
            new ConfirmPasswordChangeRequest { Code = "123456" });

        Assert.Equal(ChangePasswordStatus.InvalidConfirmationCode, result.Status);
        _authCodeHasher.DidNotReceive().Verify(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
        await _authTransaction.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmPasswordChangeAsync_WhenCodeIsValid_ChangesPasswordAndRevokesSessions()
    {
        var user = TestEntityFactory.CreateUser();
        user.PendingPasswordHash = "new-password-hash";
        user.PasswordResetCodeHash = "confirmation-code-hash";
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        ConfigureAuthTransaction();
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _authCodeHasher
            .Verify("123456", $"password-change:{user.Id:N}", "confirmation-code-hash")
            .Returns(true);

        var result = await _service.ConfirmPasswordChangeAsync(
            user.Id,
            new ConfirmPasswordChangeRequest { Code = "123456" },
            cancellationToken);

        Assert.Equal(ChangePasswordStatus.PasswordChanged, result.Status);
        Assert.Equal("new-password-hash", user.PasswordHash);
        Assert.Null(user.PendingPasswordHash);
        Assert.Null(user.PasswordResetCodeHash);
        Assert.Null(user.PasswordResetCodeExpiresAt);
        Assert.NotNull(user.UpdatedAt);
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

    private static StartPasswordChangeRequest CreateStartRequest()
    {
        return new StartPasswordChangeRequest
        {
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "NewPassword123!"
        };
    }
}
