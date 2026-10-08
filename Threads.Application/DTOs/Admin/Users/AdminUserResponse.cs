using Threads.Application.DTOs.Users;

namespace Threads.Application.DTOs.Admin;

public sealed class AdminUserResponse : UserResponse
{
    public bool IsBlocked { get; init; }
}
