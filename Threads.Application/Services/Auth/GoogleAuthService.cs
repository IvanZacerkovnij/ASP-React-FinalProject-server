using Threads.Application.DTOs.Auth.Requests;
using Threads.Application.DTOs.Auth.Responses;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Auth;

public sealed class GoogleAuthService(
    IGoogleTokenValidator tokenValidator,
    IUserRepository userRepository,
    IAuthTransaction authTransaction,
    SessionService sessionService)
{
    public async Task<AuthResponse?> LoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = await tokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return null;
        }

        var user = await userRepository.GetByGoogleSubjectAsync(identity.Subject, cancellationToken);
        if (user is not null)
        {
            return CanLogin(user)
                ? await sessionService.CreateAuthResponseAsync(user, cancellationToken)
                : null;
        }

        var email = AuthInputNormalizer.NormalizeEmail(identity.Email);
        if (await userRepository.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Sign in to your existing account and link Google before using Google login.");
        }

        if (!identity.IsEmailAuthoritative)
        {
            throw new RequestValidationException("Register and verify this email first, then link Google from your account.");
        }

        return await authTransaction.ExecuteAsync(async token =>
        {
            var newUser = new User
            {
                Email = email,
                Username = $"user_{Guid.NewGuid():N}",
                DisplayName = identity.DisplayName is { Length: <= 100 } ? identity.DisplayName : null,
                GoogleSubject = identity.Subject,
                IsVerified = true
            };
            await userRepository.AddAsync(newUser, token);
            return await sessionService.CreateAuthResponseAsync(newUser, token);
        }, cancellationToken);
    }

    public async Task<bool> LinkAsync(
        Guid userId,
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var identity = await tokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return false;
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !CanLogin(user))
        {
            return false;
        }

        if (!await userRepository.TryLinkGoogleAsync(userId, identity.Subject, cancellationToken))
        {
            throw new ConflictException("This account already has a different Google account linked.");
        }

        return true;
    }

    private static bool CanLogin(User user) =>
        user is { IsActive: true, IsVerified: true, DeletedAt: null };
}
