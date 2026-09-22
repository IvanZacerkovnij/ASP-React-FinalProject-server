namespace Threads.Application.DTOs.Users;

public sealed class UserSummaryReadModel
{
    public Guid Id { get; init; }

    public required string Username { get; init; }

    public string? DisplayName { get; init; }

    public string? LocationPlaceId { get; init; }

    public string? LocationName { get; init; }

    public string? LocationCountry { get; init; }

    public double? LocationLatitude { get; init; }

    public double? LocationLongitude { get; init; }

    public string? AvatarObjectKey { get; init; }

    public bool IsVerified { get; init; }
}
