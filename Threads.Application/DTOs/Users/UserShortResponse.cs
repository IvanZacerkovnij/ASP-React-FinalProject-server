namespace Threads.Application.DTOs.Users;

public class UserShortResponse
{
    public Guid Id { get; init; }

    public required string Username { get; init; }

    public string? DisplayName { get; init; }

    public string? AvatarUrl { get; init; }

    public bool IsVerified { get; init; }
}