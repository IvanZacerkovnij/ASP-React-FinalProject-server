using Threads.Application.DTOs.Users;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.DTOs.Users;

public sealed class UserRoleContractTests
{
    [Theory]
    [InlineData(UserRole.User, "User")]
    [InlineData(UserRole.Admin, "Admin")]
    public void Serialize_ReturnsRoleName(UserRole role, string expected)
    {
        Assert.Equal(expected, UserRoleContract.Serialize(role));
    }
}
