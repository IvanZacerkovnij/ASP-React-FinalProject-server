using Microsoft.Extensions.Configuration;
using Threads.Infrastructure.Exceptions;
using Threads.Infrastructure.Security;

namespace Threads.Infrastructure.IntegrationTests.Security;

public sealed class GoogleTokenValidatorTests : IDisposable
{
    private readonly GoogleTokenTestIssuer issuer = new();

    [Theory]
    [InlineData("https://accounts.google.com")]
    [InlineData("accounts.google.com")]
    public async Task ValidateAsync_ValidSignedToken_ReturnsGoogleIdentity(string tokenIssuer)
    {
        var identity = await CreateValidator().ValidateAsync(issuer.CreateToken(issuer: tokenIssuer));

        Assert.NotNull(identity);
        Assert.Equal("google-user-123", identity.Subject);
        Assert.Equal("googleuser@gmail.com", identity.Email);
        Assert.Equal("Google User", identity.DisplayName);
        Assert.True(identity.IsEmailAuthoritative);
    }

    [Theory]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("expired")]
    [InlineData("email")]
    [InlineData("subject")]
    [InlineData("azp")]
    [InlineData("signature")]
    [InlineData("malformed")]
    public async Task ValidateAsync_InvalidToken_ReturnsNull(string failure)
    {
        using var otherIssuer = new GoogleTokenTestIssuer();
        var token = failure switch
        {
            "audience" => issuer.CreateToken(audience: "another-client"),
            "issuer" => issuer.CreateToken(issuer: "https://attacker.example"),
            "expired" => issuer.CreateToken(expired: true),
            "email" => issuer.CreateToken(emailVerified: false),
            "subject" => issuer.CreateToken(subject: ""),
            "azp" => issuer.CreateToken(authorizedParty: "another-client"),
            "signature" => otherIssuer.CreateToken(),
            _ => "invalid-token"
        };

        Assert.Null(await CreateValidator().ValidateAsync(token));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("company.example", true)]
    public async Task ValidateAsync_ExternalEmail_RequiresHostedDomainForAuthoritativeEmail(
        string? hostedDomain,
        bool expected)
    {
        var identity = await CreateValidator().ValidateAsync(
            issuer.CreateToken(email: "user@company.example", hostedDomain: hostedDomain));

        Assert.NotNull(identity);
        Assert.Equal(expected, identity.IsEmailAuthoritative);
    }

    [Fact]
    public async Task ValidateAsync_MissingClientId_ThrowsConfigurationException()
    {
        var validator = new GoogleTokenValidator(new ConfigurationBuilder().Build(), issuer.ConfigurationManager);
        await Assert.ThrowsAsync<InfrastructureConfigurationException>(() => validator.ValidateAsync(issuer.CreateToken()));
    }

    private GoogleTokenValidator CreateValidator() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Google:ClientId"] = GoogleTokenTestIssuer.ClientId
        }).Build(),
        issuer.ConfigurationManager);

    public void Dispose() => issuer.Dispose();
}
