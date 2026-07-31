namespace Threads.Application.DTOs.Auth;

public sealed class RegisterRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
}