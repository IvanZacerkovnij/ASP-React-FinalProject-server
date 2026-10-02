using Microsoft.Extensions.Configuration;
using Threads.Infrastructure.Security;

namespace Threads.Infrastructure.IntegrationTests.Security;

public sealed class AuthCodeHasherTests
{
    [Fact]
    public void HashAndVerify_IsolateIdenticalCodesByContext()
    {
        var hasher = CreateHasher();
        const string code = "123456";
        var resetHash = hasher.Hash(code, "password-reset:user-one");
        var changeHash = hasher.Hash(code, "password-change:user-one");

        Assert.NotEqual(resetHash, changeHash);
        Assert.True(hasher.Verify(code, "password-reset:user-one", resetHash));
        Assert.False(hasher.Verify(code, "password-change:user-one", resetHash));
        Assert.False(hasher.Verify("654321", "password-reset:user-one", resetHash));
        Assert.False(hasher.Verify(code, "password-reset:user-one", "not-base64"));
    }

    private static AuthCodeHasher CreateHasher()
    {
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthCodes:HashKey"] = key
            })
            .Build();

        return new AuthCodeHasher(configuration);
    }
}
