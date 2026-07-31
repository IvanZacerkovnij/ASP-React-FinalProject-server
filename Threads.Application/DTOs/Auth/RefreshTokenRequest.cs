namespace Threads.Application.DTOs.Auth;

public class RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}