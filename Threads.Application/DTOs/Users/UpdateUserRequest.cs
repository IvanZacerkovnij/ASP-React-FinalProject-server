using Threads.Application.DTOs.Locations;

namespace Threads.Application.DTOs.Users;

public class UpdateUserRequest
{
    public string? DisplayName { get; init; }

    public string? Bio { get; init; }

    public bool RemoveLocation { get; init; }

    public LocationRequest? Location { get; init; }

    public bool RemoveAvatar { get; init; }

    public bool RemoveBanner { get; init; }
}
