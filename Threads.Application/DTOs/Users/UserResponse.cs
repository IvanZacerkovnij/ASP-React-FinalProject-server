using Threads.Application.DTOs.Locations;
using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Users;

public class UserResponse
{
    public Guid Id { get; init; }

    public required string Username { get; init; }

    public string? Email { get; init; }

    public string? DisplayName { get; init; }

    public string? Bio { get; init; }

    public DateOnly? BirthDate { get; init; }

    public VisibilityLevel BirthDateVisibility { get; init; } = VisibilityLevel.OnlyMe;

    public VisibilityLevel BirthYearVisibility { get; init; } = VisibilityLevel.OnlyMe;

    public LocationResponse? Location { get; init; }

    public string? AvatarUrl { get; init; }

    public string? BannerUrl { get; init; }

    public int FollowersCount { get; init; }

    public int FollowingCount { get; init; }

    public int PostsCount { get; init; }

    public bool IsFollowedByCurrentUser { get; init; }

    public bool IsVerified { get; init; }

    public string Role { get; init; } = UserRoleContract.User;

    public DateTimeOffset CreatedAt { get; init; }
}
