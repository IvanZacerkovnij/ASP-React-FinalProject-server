namespace Threads.Application.DTOs.Auth.Requests;

public class ResendVerificationCodeRequest
{
    public required string Email { get; init; }
}