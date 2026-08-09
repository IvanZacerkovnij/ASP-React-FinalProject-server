namespace Threads.Application.DTOs.Auth;

public class LogoutRequest
{
    public required string RefreshToken { get; init; }
}
