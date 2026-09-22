using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Auth;

public sealed class RegistrationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPendingRegistrationRepository _pendingRegistrationRepository;
    private readonly IAuthTransaction _authTransaction;
    private readonly IAuthEmailService _authEmailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SessionService _sessionService;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        IUserRepository userRepository,
        IPendingRegistrationRepository pendingRegistrationRepository,
        IAuthTransaction authTransaction,
        IAuthEmailService authEmailService,
        IPasswordHasher passwordHasher,
        SessionService sessionService,
        ILogger<RegistrationService> logger)
    {
        _userRepository = userRepository;
        _pendingRegistrationRepository = pendingRegistrationRepository;
        _authTransaction = authTransaction;
        _authEmailService = authEmailService;
        _passwordHasher = passwordHasher;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var registration = NormalizeRegistrationRequest(request);
        await EnsureUserDoesNotExistAsync(registration.Email, registration.Username, cancellationToken);

        var (pendingRegistration, isNew) = await ResolvePendingRegistrationAsync(
            registration.Email,
            registration.Username,
            cancellationToken);
        var code = AuthCode.Generate();

        ApplyRegistrationData(pendingRegistration, registration, code);
        await SavePendingRegistrationAsync(pendingRegistration, isNew, cancellationToken);
        await _authEmailService.SendEmailVerificationCodeAsync(
            pendingRegistration.Email,
            code,
            cancellationToken);
        _logger.LogInformation(
            "Registration verification code sent for pending registration {PendingRegistrationId}. IsNew: {IsNew}",
            pendingRegistration.Id,
            isNew);
    }

    public async Task<AuthResponse?> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AuthInputNormalizer.NormalizeEmail(request.Email);
        var pendingRegistration = await GetVerifiedPendingRegistrationAsync(
            normalizedEmail,
            request.Code,
            cancellationToken);

        if (pendingRegistration is null)
        {
            _logger.LogWarning("Email verification failed because the code or registration was invalid");
            return null;
        }

        var response = await CompleteEmailVerificationAsync(pendingRegistration, cancellationToken);
        _logger.LogInformation("Email verified and user {UserId} registered", response.UserId);
        return response;
    }

    public async Task<bool> ResendVerificationCodeAsync(
        ResendVerificationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AuthInputNormalizer.NormalizeEmail(request.Email);
        var pendingRegistration = await GetActivePendingRegistrationByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (pendingRegistration is null)
        {
            return false;
        }

        await RefreshEmailVerificationCodeAsync(pendingRegistration, cancellationToken);
        _logger.LogInformation(
            "Verification code resent for pending registration {PendingRegistrationId}",
            pendingRegistration.Id);
        return true;
    }

    private async Task<(PendingRegistration PendingRegistration, bool IsNew)> ResolvePendingRegistrationAsync(
        string normalizedEmail,
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        var pendingRegistrationByEmail = await GetActivePendingRegistrationByEmailAsync(
            normalizedEmail,
            cancellationToken);
        var pendingRegistrationByUsername = await GetActivePendingRegistrationByUsernameAsync(
            normalizedUsername,
            cancellationToken);

        if (pendingRegistrationByEmail is not null &&
            pendingRegistrationByUsername is not null &&
            pendingRegistrationByEmail.Id != pendingRegistrationByUsername.Id)
        {
            throw new ConflictException(
                "Email or username is already used in another pending registration.");
        }

        var pendingRegistration = pendingRegistrationByEmail
            ?? pendingRegistrationByUsername
            ?? new PendingRegistration();
        var isNew = pendingRegistrationByEmail is null && pendingRegistrationByUsername is null;

        return (pendingRegistration, isNew);
    }

    private static RegistrationData NormalizeRegistrationRequest(RegisterRequest request)
    {
        var normalizedEmail = AuthInputNormalizer.NormalizeEmail(request.Email);
        var normalizedUsername = AuthInputNormalizer.NormalizeUsername(request.Username);
        var normalizedPassword = AuthInputNormalizer.NormalizePassword(
            request.Password,
            nameof(request.Password));

        if (Guid.TryParse(normalizedUsername, out _))
        {
            throw new RequestValidationException("Username must not be a GUID.");
        }

        return new RegistrationData(
            normalizedEmail,
            normalizedUsername,
            normalizedPassword,
            AuthInputNormalizer.NormalizeOptional(request.DisplayName));
    }

    private void ApplyRegistrationData(
        PendingRegistration pendingRegistration,
        RegistrationData registration,
        string code)
    {
        pendingRegistration.Email = registration.Email;
        pendingRegistration.Username = registration.Username;
        pendingRegistration.PasswordHash = _passwordHasher.HashPassword(registration.Password);
        pendingRegistration.DisplayName = registration.DisplayName;
        AuthCode.SetEmailVerificationCode(pendingRegistration, code);
        pendingRegistration.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task SavePendingRegistrationAsync(
        PendingRegistration pendingRegistration,
        bool isNew,
        CancellationToken cancellationToken)
    {
        if (isNew)
        {
            await _pendingRegistrationRepository.AddAsync(pendingRegistration, cancellationToken);
            return;
        }

        await _pendingRegistrationRepository.UpdateAsync(pendingRegistration, cancellationToken);
    }

    private async Task<AuthResponse> CompleteEmailVerificationAsync(
        PendingRegistration pendingRegistration,
        CancellationToken cancellationToken)
    {
        return await _authTransaction.ExecuteAsync(async token =>
        {
            await EnsureUserDoesNotExistAsync(
                pendingRegistration.Email,
                pendingRegistration.Username,
                token);

            var user = CreateVerifiedUser(pendingRegistration);
            await _userRepository.AddAsync(user, token);
            await _pendingRegistrationRepository.DeleteAsync(pendingRegistration, token);

            return await _sessionService.CreateAuthResponseAsync(user, token);
        }, cancellationToken);
    }

    private static User CreateVerifiedUser(PendingRegistration pendingRegistration)
    {
        return new User
        {
            Email = pendingRegistration.Email,
            Username = pendingRegistration.Username,
            PasswordHash = pendingRegistration.PasswordHash,
            DisplayName = pendingRegistration.DisplayName,
            IsVerified = true,
            IsActive = true
        };
    }

    private async Task RefreshEmailVerificationCodeAsync(
        PendingRegistration pendingRegistration,
        CancellationToken cancellationToken)
    {
        var code = AuthCode.Generate();
        AuthCode.SetEmailVerificationCode(pendingRegistration, code);
        pendingRegistration.UpdatedAt = DateTimeOffset.UtcNow;

        await _pendingRegistrationRepository.UpdateAsync(pendingRegistration, cancellationToken);
        await _authEmailService.SendEmailVerificationCodeAsync(
            pendingRegistration.Email,
            code,
            cancellationToken);
    }

    private async Task EnsureUserDoesNotExistAsync(
        string normalizedEmail,
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        var existingUserByEmail = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (existingUserByEmail is not null)
        {
            throw new ConflictException("User with this email already exists.");
        }

        var existingUserByUsername = await _userRepository.GetByUsernameAsync(
            normalizedUsername,
            cancellationToken);

        if (existingUserByUsername is not null)
        {
            throw new ConflictException("User with this username already exists.");
        }
    }

    private async Task<PendingRegistration?> GetActivePendingRegistrationByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var pendingRegistration = await _pendingRegistrationRepository.GetByEmailAsync(
            email,
            cancellationToken);

        return await RemovePendingRegistrationIfExpiredAsync(pendingRegistration, cancellationToken);
    }

    private async Task<PendingRegistration?> GetActivePendingRegistrationByUsernameAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var pendingRegistration = await _pendingRegistrationRepository.GetByUsernameAsync(
            username,
            cancellationToken);

        return await RemovePendingRegistrationIfExpiredAsync(pendingRegistration, cancellationToken);
    }

    private async Task<PendingRegistration?> RemovePendingRegistrationIfExpiredAsync(
        PendingRegistration? pendingRegistration,
        CancellationToken cancellationToken)
    {
        if (pendingRegistration is null)
        {
            return null;
        }

        if (pendingRegistration.VerificationCodeExpiresAt > DateTimeOffset.UtcNow)
        {
            return pendingRegistration;
        }

        await _pendingRegistrationRepository.DeleteAsync(pendingRegistration, cancellationToken);
        return null;
    }

    private async Task<PendingRegistration?> GetVerifiedPendingRegistrationAsync(
        string normalizedEmail,
        string code,
        CancellationToken cancellationToken)
    {
        var pendingRegistration = await GetActivePendingRegistrationByEmailAsync(
            normalizedEmail,
            cancellationToken);

        return AuthCode.IsEmailVerificationCodeValid(pendingRegistration, code)
            ? pendingRegistration
            : null;
    }

    private sealed record RegistrationData(
        string Email,
        string Username,
        string Password,
        string? DisplayName);
}
