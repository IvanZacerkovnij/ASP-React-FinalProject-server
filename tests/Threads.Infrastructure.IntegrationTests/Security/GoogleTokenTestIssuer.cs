using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Threads.Infrastructure.IntegrationTests.Security;

internal sealed class GoogleTokenTestIssuer : IDisposable
{
    public const string ClientId = "google-tests.apps.googleusercontent.com";
    private readonly RSA signingRsa = RSA.Create(2048);
    private readonly RsaSecurityKey signingKey;

    public GoogleTokenTestIssuer()
    {
        signingKey = new RsaSecurityKey(signingRsa) { KeyId = "test-google-key" };
        var configuration = new OpenIdConnectConfiguration { Issuer = "https://accounts.google.com" };
        configuration.SigningKeys.Add(new RsaSecurityKey(signingRsa.ExportParameters(false))
        {
            KeyId = signingKey.KeyId
        });
        ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    public IConfigurationManager<OpenIdConnectConfiguration> ConfigurationManager { get; }

    public string CreateToken(
        string subject = "google-user-123",
        string email = "googleuser@gmail.com",
        string audience = ClientId,
        string issuer = "https://accounts.google.com",
        bool emailVerified = true,
        bool expired = false,
        string? hostedDomain = null,
        string? authorizedParty = null)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("email", email),
            new("name", "Google User"),
            new("email_verified", emailVerified ? "true" : "false", ClaimValueTypes.Boolean)
        };
        if (hostedDomain is not null)
        {
            claims.Add(new Claim("hd", hostedDomain));
        }

        if (authorizedParty is not null)
        {
            claims.Add(new Claim("azp", authorizedParty));
        }

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            DateTime.UtcNow.AddMinutes(-10),
            expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose() => signingRsa.Dispose();
}
