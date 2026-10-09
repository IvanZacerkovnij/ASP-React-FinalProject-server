using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Users;

public static class UserRoleContract
{
    public const string User = nameof(UserRole.User);
    public const string Admin = nameof(UserRole.Admin);

    public static string Serialize(UserRole role)
    {
        return role == UserRole.Admin ? Admin : User;
    }
}
