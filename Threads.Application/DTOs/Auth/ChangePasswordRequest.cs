namespace Threads.Application.DTOs.Auth;

public sealed class ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }
    public string? NewPassword { get; init; }
    public string? Code { get; init; }
}
