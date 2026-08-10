namespace Threads.Application.DTOs.Users;

public class UpdateUserRequest
{
    public string? DisplayName { get; init; }

    public string? Bio { get; init; }

    public string? Location { get; init; }

    public bool RemoveAvatar { get; init; }

    public bool RemoveBanner { get; init; }
}
