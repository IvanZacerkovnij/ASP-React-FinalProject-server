using Threads.Application.DTOs.Locations;

namespace Threads.Application.DTOs.Users;

public class UserResponse
{
    public Guid Id { get; init; }

    public required string Username { get; init; }

    public string? DisplayName { get; init; }

    public string? Bio { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public LocationResponse? Location { get; init; }

    public string? AvatarUrl { get; init; }

    public string? BannerUrl { get; init; }

    public int FollowersCount { get; init; }

    public int FollowingCount { get; init; }

    public int PostsCount { get; init; }

    public bool IsFollowedByCurrentUser { get; init; }

    public bool IsVerified { get; init; }

    public DateTime CreatedAt { get; init; }
}
