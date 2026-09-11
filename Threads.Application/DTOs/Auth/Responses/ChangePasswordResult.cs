namespace Threads.Application.DTOs.Auth.Responses;

public sealed class ChangePasswordResult
{
    public required ChangePasswordStatus Status { get; init; }
}
