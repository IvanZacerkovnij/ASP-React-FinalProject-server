namespace Threads.Application.DTOs.Auth;

public class AuthResponse
{
    public Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public DateTime AccessTokenExpiresAt { get; init; }
}