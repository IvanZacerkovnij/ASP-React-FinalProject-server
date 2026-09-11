namespace Threads.Application.DTOs.Auth.Requests;

public sealed class ResetPasswordRequest
{
    public required string Email { get; init; }
    public required string Code { get; init; }
    public required string NewPassword { get; init; }
}
