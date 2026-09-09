namespace Threads.Application.DTOs.Users;

public sealed class UserProfileReadModel
{
    public Guid Id { get; init; }

    public required string Username { get; init; }

    public string? DisplayName { get; init; }

    public string? Bio { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public string? LocationPlaceId { get; init; }

    public string? LocationName { get; init; }

    public string? LocationCountry { get; init; }

    public double? LocationLatitude { get; init; }

    public double? LocationLongitude { get; init; }

    public string? AvatarObjectKey { get; init; }

    public string? BannerObjectKey { get; init; }

    public int FollowersCount { get; init; }

    public int FollowingCount { get; init; }

    public int PostsCount { get; init; }

    public bool IsVerified { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
