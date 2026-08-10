using System.Security.Cryptography;
using System.Text;
using AutoMapper;
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

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordResetEmailService _passwordResetEmailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordResetEmailService passwordResetEmailService,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordResetEmailService = passwordResetEmailService;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mapper = mapper;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedUsername = NormalizeUsername(request.Username);
        var normalizedPassword = NormalizePassword(request.Password, nameof(request.Password));

        if (Guid.TryParse(normalizedUsername, out _))
        {
            throw new InvalidOperationException("Username must not be a GUID.");
        }

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

        var user = _mapper.Map<User>(request);
        user.Email = normalizedEmail;
        user.Username = normalizedUsername;
        user.PasswordHash = _passwordHasher.HashPassword(normalizedPassword);

        await _userRepository.AddAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmailOrUsername = NormalizeRequiredValue(
            request.EmailOrUsername,
            nameof(request.EmailOrUsername),
            "Email or username is required.");
        var normalizedPassword = NormalizePassword(request.Password, nameof(request.Password));
        var user = await GetUserByEmailOrUsernameAsync(normalizedEmailOrUsername, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(normalizedPassword, user.PasswordHash);

        if (!isPasswordValid)
        {
            return null;
        }

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var refreshTokenHash = HashRefreshToken(NormalizeRefreshToken(request.RefreshToken));

        var currentRefreshToken = await _refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);

        if (currentRefreshToken is null || !currentRefreshToken.IsActive || !currentRefreshToken.User.IsActive)
        {
            return null;
        }

        currentRefreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await _refreshTokenRepository.UpdateAsync(currentRefreshToken, cancellationToken);

        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenEntity = new RefreshToken
        {
            TokenHash = HashRefreshToken(newRefreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenLifetimeDays),
            UserId = currentRefreshToken.UserId
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);

        return CreateAuthResponse(currentRefreshToken.User, newRefreshToken);
    }

    public async Task<bool> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var refreshTokenHash = HashRefreshToken(NormalizeRefreshToken(request.RefreshToken));

        var refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
        {
            return false;
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);

        return true;
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return;
        }

        var code = GeneratePasswordResetCode();
        user.PendingPasswordHash = null;
        SetPasswordResetCode(user, code);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _passwordResetEmailService.SendPasswordResetCodeAsync(user.Email, code, cancellationToken);
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
        var normalizedCode = NormalizePasswordResetCode(request.Code);

        var normalizedPassword = NormalizePassword(request.NewPassword, nameof(request.NewPassword));

        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (!IsPasswordResetCodeValid(user, normalizedCode))
        {
            return false;
        }

        user!.PasswordHash = _passwordHasher.HashPassword(normalizedPassword);
        ClearPasswordResetCode(user);
        user.PendingPasswordHash = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id, DateTimeOffset.UtcNow, cancellationToken);

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
            return new ChangePasswordResult
            {
                Status = ChangePasswordStatus.UserNotFound
            };
        }

        if (!_passwordHasher.VerifyPassword(normalizedCurrentPassword, user.PasswordHash))
        {
            return new ChangePasswordResult
            {
                Status = ChangePasswordStatus.InvalidCurrentPassword
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var normalizedCode = NormalizePasswordResetCode(request.Code);

            if (string.IsNullOrWhiteSpace(user.PendingPasswordHash))
            {
                return new ChangePasswordResult
                {
                    Status = ChangePasswordStatus.NoPendingPasswordChange
                };
            }

            if (!IsPasswordResetCodeValid(user, normalizedCode))
            {
                return new ChangePasswordResult
                {
                    Status = ChangePasswordStatus.InvalidConfirmationCode
                };
            }

            user.PasswordHash = user.PendingPasswordHash;
            user.PendingPasswordHash = null;
            ClearPasswordResetCode(user);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id, DateTimeOffset.UtcNow, cancellationToken);

            return new ChangePasswordResult
            {
                Status = ChangePasswordStatus.PasswordChanged
            };
        }

        var normalizedNewPassword = NormalizePassword(request.NewPassword, nameof(request.NewPassword));

        if (_passwordHasher.VerifyPassword(normalizedNewPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("New password must be different from the current password.");
        }

        var code = GeneratePasswordResetCode();
        user.PendingPasswordHash = _passwordHasher.HashPassword(normalizedNewPassword);
        SetPasswordResetCode(user, code);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _passwordResetEmailService.SendPasswordChangeCodeAsync(user.Email, code, cancellationToken);

        return new ChangePasswordResult
        {
            Status = ChangePasswordStatus.ConfirmationCodeSent
        };
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

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenLifetimeDays),
            UserId = user.Id
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);

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

    private static string NormalizePassword(string password, string paramName)
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
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Reset code is required.", nameof(code));
        }

        var normalizedCode = code.Trim();

        if (normalizedCode.Length != 6 || normalizedCode.Any(character => !char.IsDigit(character)))
        {
            throw new ArgumentException("Reset code must contain exactly 6 digits.", nameof(code));
        }

        return normalizedCode;
    }

    private static string GeneratePasswordResetCode()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6");
    }

    private static void SetPasswordResetCode(User user, string code)
    {
        user.PasswordResetCodeHash = HashPasswordResetCode(code);
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(PasswordResetCodeLifetimeMinutes);
    }

    private static void ClearPasswordResetCode(User user)
    {
        user.PasswordResetCodeHash = null;
        user.PasswordResetCodeExpiresAt = null;
    }

    private static string HashPasswordResetCode(string code)
    {
        var normalizedCode = NormalizePasswordResetCode(code);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));
        return Convert.ToBase64String(bytes);
    }

    private static bool IsPasswordResetCodeValid(User? user, string code)
    {
        if (user is null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(user.PasswordResetCodeHash) ||
            user.PasswordResetCodeExpiresAt is null ||
            user.PasswordResetCodeExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        return user.PasswordResetCodeHash == HashPasswordResetCode(code);
    }
}
