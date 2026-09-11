namespace Threads.Application.DTOs.Auth.Requests;

public sealed class LoginRequest
{
    public required string EmailOrUsername { get; init; }
    public required string Password { get; init; }
}