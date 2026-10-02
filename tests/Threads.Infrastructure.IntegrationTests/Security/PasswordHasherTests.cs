using Threads.Infrastructure.Security;

namespace Threads.Infrastructure.IntegrationTests.Security;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashPassword_ProducesSaltedHashThatOnlyAcceptsOriginalPassword()
    {
        const string password = "CorrectPassword123!";
        var hasher = new PasswordHasher();

        var firstHash = hasher.HashPassword(password);
        var secondHash = hasher.HashPassword(password);

        Assert.NotEqual(password, firstHash);
        Assert.NotEqual(firstHash, secondHash);
        Assert.True(hasher.VerifyPassword(password, firstHash));
        Assert.False(hasher.VerifyPassword("WrongPassword123!", firstHash));
    }
}
