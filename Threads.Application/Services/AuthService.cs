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

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mapper = mapper;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        var normalizedUsername = request.Username.Trim();

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
        user.PasswordHash = _passwordHasher.HashPassword(request.Password);

        await _userRepository.AddAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByEmailOrUsernameAsync(request.EmailOrUsername, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);

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
        var refreshTokenHash = HashRefreshToken(request.RefreshToken);

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
        var refreshTokenHash = HashRefreshToken(request.RefreshToken);

        var refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
        {
            return false;
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);

        return true;
    }

    private async Task<User?> GetUserByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken)
    {
        var normalizedValue = emailOrUsername.Trim();

        if (normalizedValue.Contains('@'))
        {
            return await _userRepository.GetByEmailAsync(normalizedValue, cancellationToken);
        }

        return await _userRepository.GetByUsernameAsync(normalizedValue, cancellationToken);
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
}
