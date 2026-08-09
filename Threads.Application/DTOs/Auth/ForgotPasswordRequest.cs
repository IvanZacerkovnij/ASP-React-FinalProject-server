namespace Threads.Application.DTOs.Auth;

public sealed class ForgotPasswordRequest
{
    public required string Email { get; init; }
}
