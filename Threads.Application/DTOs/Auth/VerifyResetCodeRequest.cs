namespace Threads.Application.DTOs.Auth;

public sealed class VerifyResetCodeRequest
{
    public required string Email { get; init; }
    public required string Code { get; init; }
}
