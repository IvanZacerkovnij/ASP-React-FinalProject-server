namespace Threads.Application.DTOs.Auth.Requests;

public sealed class VerifyResetCodeRequest
{
    public required string Email { get; init; }
    public required string Code { get; init; }
}
