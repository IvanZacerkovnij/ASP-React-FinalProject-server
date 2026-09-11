namespace Threads.Application.DTOs.Auth.Requests;

public class RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}