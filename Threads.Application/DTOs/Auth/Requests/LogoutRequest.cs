namespace Threads.Application.DTOs.Auth.Requests;

public class LogoutRequest
{
    public required string RefreshToken { get; init; }
}
