namespace Threads.Application.DTOs.Auth;

public class ResendVerificationCodeRequest
{
    public required string Email { get; init; }
}