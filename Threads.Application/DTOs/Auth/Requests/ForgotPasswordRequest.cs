namespace Threads.Application.DTOs.Auth.Requests;

public sealed class ForgotPasswordRequest
{
    public required string Email { get; init; }
}
