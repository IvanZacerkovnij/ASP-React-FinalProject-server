namespace Threads.Application.Interfaces.Auth;

public interface IGoogleTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record GoogleIdentity(string Subject, string Email, string? DisplayName, bool IsEmailAuthoritative);
