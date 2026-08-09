namespace Threads.Application.DTOs.Auth;

public sealed class ResetPasswordRequest
{
    public required string Email { get; init; }
    public required string Code { get; init; }
    public required string NewPassword { get; init; }
}
