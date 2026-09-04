using System.Security.Cryptography;
using System.Text;
using Threads.Application.DTOs.Auth;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class AuthService : IAuthService
{
    private const int RefreshTokenLifetimeDays = 7;
    private const int PasswordResetCodeLifetimeMinutes = 15;
    private const int EmailVerificationCodeLifetimeMinutes = 15;

    private readonly IUserRepository _userRepository;
    private readonly IPendingRegistrationRepository _pendingRegistrationRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthEmailService _authEmailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        IUserRepository userRepository,
        IPendingRegistrationRepository pendingRegistrationRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthEmailService authEmailService,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _pendingRegistrationRepository = pendingRegistrationRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _authEmailService = authEmailService;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var registration = NormalizeRegistrationRequest(request);
        await EnsureUserDoesNotExistAsync(registration.Email, registration.Username, cancellationToken);

        var (pendingRegistration, isNew) = await ResolvePendingRegistrationAsync(
            registration.Email,
            registration.Username,
            cancellationToken);
        var code = GenerateCode();

        ApplyRegistrationData(pendingRegistration, registration, code);
        await SavePendingRegistrationAsync(pendingRegistration, isNew, cancellationToken);
        await _authEmailService.SendEmailVerificationCodeAsync(
            pendingRegistration.Email,
            code,
            cancellationToken);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedIdentity = NormalizeRequiredValue(
            request.EmailOrUsername,
            nameof(request.EmailOrUsername),
            "Email or username is required.");
        var normalizedPassword = NormalizePassword(request.Password, nameof(request.Password));
        var user = await GetUserByEmailOrUsernameAsync(normalizedIdentity, cancellationToken);

        if (!CanUserLogin(user))
        {
            return null;
        }

        if (!_passwordHasher.VerifyPassword(normalizedPassword, user!.PasswordHash))
        {
            return null;
        }

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentRefreshToken = await GetActiveRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (currentRefreshToken is null)
        {
            return null;
        }

        await RevokeRefreshTokenAsync(currentRefreshToken, cancellationToken);
        var newRefreshToken = await IssueRefreshTokenAsync(currentRefreshToken.UserId, cancellationToken);

        return CreateAuthResponse(currentRefreshToken.User, newRefreshToken);
    }

    public async Task<bool> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var refreshToken = await GetStoredRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
        {
            return false;
        }

        await RevokeRefreshTokenAsync(refreshToken, cancellationToken);

        return true;
    }

    public async Task<AuthResponse?> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var pendingRegistration = await GetVerifiedPendingRegistrationAsync(
            normalizedEmail,
            request.Code,
            cancellationToken);

        if (pendingRegistration is null)
        {
            return null;
        }

        return await CompleteEmailVerificationAsync(pendingRegistration, cancellationToken);
    }

    public async Task<bool> ResendVerificationCodeAsync(
        ResendVerificationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var pendingRegistration = await GetActivePendingRegistrationByEmailAsync(NormalizeEmail(request.Email), cancellationToken);

        if (pendingRegistration is null)
        {
            return false;
        }

        await RefreshEmailVerificationCodeAsync(pendingRegistration, cancellationToken);
        return true;
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            return;
        }

        await SendPasswordResetCodeAsync(user, cancellationToken);
    }

    public async Task<bool> VerifyResetCodeAsync(
        VerifyResetCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);

        return IsPasswordResetCodeValid(user, request.Code);
    }

    public async Task<bool> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedPassword = NormalizePassword(request.NewPassword, nameof(request.NewPassword));
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (!IsPasswordResetCodeValid(user, request.Code))
        {
            return false;
        }

        await ApplyPasswordChangeAsync(user!, _passwordHasher.HashPassword(normalizedPassword), cancellationToken);
        return true;
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrentPassword = NormalizePassword(request.CurrentPassword, nameof(request.CurrentPassword));
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return CreateChangePasswordResult(ChangePasswordStatus.UserNotFound);
        }

        if (!_passwordHasher.VerifyPassword(normalizedCurrentPassword, user.PasswordHash))
        {
            return CreateChangePasswordResult(ChangePasswordStatus.InvalidCurrentPassword);
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            return await ConfirmPasswordChangeAsync(user, request.Code, cancellationToken);
        }

        return await StartPasswordChangeAsync(user, request.NewPassword, cancellationToken);
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
            throw new InvalidOperationException("Email or username is already used in another pending registration.");
        }

        var pendingRegistration = pendingRegistrationByEmail
            ?? pendingRegistrationByUsername
            ?? new PendingRegistration();
        var isNew = pendingRegistrationByEmail is null && pendingRegistrationByUsername is null;

        return (pendingRegistration, isNew);
    }

    private static RegistrationData NormalizeRegistrationRequest(RegisterRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedUsername = NormalizeUsername(request.Username);
        var normalizedPassword = NormalizePassword(request.Password, nameof(request.Password));

        if (Guid.TryParse(normalizedUsername, out _))
        {
            throw new InvalidOperationException("Username must not be a GUID.");
        }

        return new RegistrationData(
            normalizedEmail,
            normalizedUsername,
            normalizedPassword,
            NormalizeOptionalValue(request.DisplayName));
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
        SetEmailVerificationCode(pendingRegistration, code);
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
        await EnsureUserDoesNotExistAsync(
            pendingRegistration.Email,
            pendingRegistration.Username,
            cancellationToken);

        var user = CreateVerifiedUser(pendingRegistration);
        await _userRepository.AddAsync(user, cancellationToken);
        await _pendingRegistrationRepository.DeleteAsync(pendingRegistration, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
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
        var code = GenerateCode();
        SetEmailVerificationCode(pendingRegistration, code);
        pendingRegistration.UpdatedAt = DateTimeOffset.UtcNow;

        await _pendingRegistrationRepository.UpdateAsync(pendingRegistration, cancellationToken);
        await _authEmailService.SendEmailVerificationCodeAsync(
            pendingRegistration.Email,
            code,
            cancellationToken);
    }

    private async Task SendPasswordResetCodeAsync(User user, CancellationToken cancellationToken)
    {
        var code = GenerateCode();
        user.PendingPasswordHash = null;
        SetPasswordResetCode(user, code);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _authEmailService.SendPasswordResetCodeAsync(user.Email, code, cancellationToken);
    }

    private async Task<ChangePasswordResult> ConfirmPasswordChangeAsync(
        User user,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.PendingPasswordHash))
        {
            return CreateChangePasswordResult(ChangePasswordStatus.NoPendingPasswordChange);
        }

        if (!IsPasswordResetCodeValid(user, code))
        {
            return CreateChangePasswordResult(ChangePasswordStatus.InvalidConfirmationCode);
        }

        await ApplyPasswordChangeAsync(user, user.PendingPasswordHash, cancellationToken);

        return CreateChangePasswordResult(ChangePasswordStatus.PasswordChanged);
    }

    private async Task<ChangePasswordResult> StartPasswordChangeAsync(
        User user,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        var normalizedNewPassword = NormalizePassword(newPassword, nameof(ChangePasswordRequest.NewPassword));

        if (_passwordHasher.VerifyPassword(normalizedNewPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("New password must be different from the current password.");
        }

        var code = GenerateCode();
        user.PendingPasswordHash = _passwordHasher.HashPassword(normalizedNewPassword);
        SetPasswordResetCode(user, code);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _authEmailService.SendPasswordChangeCodeAsync(user.Email, code, cancellationToken);

        return CreateChangePasswordResult(ChangePasswordStatus.ConfirmationCodeSent);
    }

    private async Task ApplyPasswordChangeAsync(
        User user,
        string newPasswordHash,
        CancellationToken cancellationToken)
    {
        user.PasswordHash = newPasswordHash;
        user.PendingPasswordHash = null;
        ClearPasswordResetCode(user);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id, DateTimeOffset.UtcNow, cancellationToken);
    }

    private async Task<User?> GetUserByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken)
    {
        var normalizedValue = emailOrUsername.Trim();

        if (normalizedValue.Contains('@'))
        {
            return await _userRepository.GetByEmailAsync(NormalizeEmail(normalizedValue), cancellationToken);
        }

        return await _userRepository.GetByUsernameAsync(NormalizeUsername(normalizedValue), cancellationToken);
    }

    private async Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);

        return user is { IsActive: true }
            ? user
            : null;
    }

    private static bool CanUserLogin(User? user)
    {
        return user is { IsActive: true, IsVerified: true };
    }

    private static ChangePasswordResult CreateChangePasswordResult(ChangePasswordStatus status)
    {
        return new ChangePasswordResult
        {
            Status = status
        };
    }

    private async Task EnsureUserDoesNotExistAsync(
        string normalizedEmail,
        string normalizedUsername,
        CancellationToken cancellationToken)
    {
        var existingUserByEmail = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUserByEmail is not null)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        var existingUserByUsername = await _userRepository.GetByUsernameAsync(normalizedUsername, cancellationToken);
        if (existingUserByUsername is not null)
        {
            throw new InvalidOperationException("User with this username already exists.");
        }
    }

    private async Task<PendingRegistration?> GetActivePendingRegistrationByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var pendingRegistration = await _pendingRegistrationRepository.GetByEmailAsync(email, cancellationToken);
        return await RemovePendingRegistrationIfExpiredAsync(pendingRegistration, cancellationToken);
    }

    private async Task<PendingRegistration?> GetActivePendingRegistrationByUsernameAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var pendingRegistration = await _pendingRegistrationRepository.GetByUsernameAsync(username, cancellationToken);
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
        var pendingRegistration = await GetActivePendingRegistrationByEmailAsync(normalizedEmail, cancellationToken);

        return IsEmailVerificationCodeValid(pendingRegistration, code)
            ? pendingRegistration
            : null;
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var refreshToken = await IssueRefreshTokenAsync(user.Id, cancellationToken);
        return CreateAuthResponse(user, refreshToken);
    }

    private AuthResponse CreateAuthResponse(User user, string refreshToken)
    {
        return new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            AccessToken = _tokenService.GenerateAccessToken(user),
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = _tokenService.GetAccessTokenExpiresAtUtc()
        };
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }

    private static string NormalizeEmail(string email)
    {
        var normalizedEmail = NormalizeRequiredValue(email, nameof(email), "Email is required.")
            .ToLowerInvariant();

        return normalizedEmail;
    }

    private static string NormalizeUsername(string username)
    {
        return NormalizeRequiredValue(username, nameof(username), "Username is required.")
            .ToLowerInvariant();
    }

    private static string NormalizePassword(string? password, string paramName)
    {
        return NormalizeRequiredValue(password, paramName, "Password is required.");
    }

    private static string NormalizeRefreshToken(string refreshToken)
    {
        return NormalizeRequiredValue(refreshToken, nameof(refreshToken), "Refresh token is required.");
    }

    private static string NormalizeRequiredValue(string? value, string paramName, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(errorMessage, paramName);
        }

        return value.Trim();
    }

    private static string NormalizePasswordResetCode(string code)
    {
        return NormalizeSixDigitCode(code, "Reset code");
    }

    private static string NormalizeEmailVerificationCode(string code)
    {
        return NormalizeSixDigitCode(code, "Verification code");
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string GenerateCode()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6");
    }

    private static string NormalizeSixDigitCode(string code, string codeName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException($"{codeName} is required.", nameof(code));
        }

        var normalizedCode = code.Trim();

        if (normalizedCode.Length != 6 || normalizedCode.Any(character => !char.IsDigit(character)))
        {
            throw new ArgumentException($"{codeName} must contain exactly 6 digits.", nameof(code));
        }

        return normalizedCode;
    }

    private static void SetPasswordResetCode(User user, string code)
    {
        user.PasswordResetCode = NormalizePasswordResetCode(code);
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(PasswordResetCodeLifetimeMinutes);
    }

    private static void ClearPasswordResetCode(User user)
    {
        user.PasswordResetCode = null;
        user.PasswordResetCodeExpiresAt = null;
    }

    private static void SetEmailVerificationCode(PendingRegistration pendingRegistration, string code)
    {
        pendingRegistration.VerificationCode = NormalizeEmailVerificationCode(code);
        pendingRegistration.VerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(
            EmailVerificationCodeLifetimeMinutes);
    }

    private static bool IsPasswordResetCodeValid(User? user, string code)
    {
        if (user is null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(user.PasswordResetCode) ||
            user.PasswordResetCodeExpiresAt is null ||
            user.PasswordResetCodeExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        return user.PasswordResetCode == NormalizePasswordResetCode(code);
    }

    private static bool IsEmailVerificationCodeValid(PendingRegistration? pendingRegistration, string code)
    {
        if (pendingRegistration is null || pendingRegistration.VerificationCodeExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        return pendingRegistration.VerificationCode == NormalizeEmailVerificationCode(code);
    }

    private async Task<RefreshToken?> GetStoredRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var refreshTokenHash = HashRefreshToken(NormalizeRefreshToken(refreshToken));
        return await _refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);
    }

    private async Task<RefreshToken?> GetActiveRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var storedRefreshToken = await GetStoredRefreshTokenAsync(refreshToken, cancellationToken);

        if (storedRefreshToken is null || !storedRefreshToken.IsActive || !storedRefreshToken.User.IsActive)
        {
            return null;
        }

        return storedRefreshToken;
    }

    private async Task<string> IssueRefreshTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenEntity = new RefreshToken
        {
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenLifetimeDays),
            UserId = userId
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        return refreshToken;
    }

    private async Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);
    }

    private sealed record RegistrationData(
        string Email,
        string Username,
        string Password,
        string? DisplayName);
}
