using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Users;

public static class UserRoleContract
{
    public const string User = "USER";
    public const string Admin = "ADMIN";

    public static string Serialize(UserRole role)
    {
        return role == UserRole.Admin ? Admin : User;
    }
}
