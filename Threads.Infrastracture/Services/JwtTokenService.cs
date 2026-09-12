using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Threads.Application.Interfaces.Security;
using Threads.Domain.Entities;
using Threads.Infrastracture.Exceptions;

namespace Threads.Infrastracture.Services;

public class JwtTokenService : ITokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;
    private readonly int _accessTokenLifetimeMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer = GetRequiredConfigurationValue(configuration, "Jwt:Issuer");
        _audience = GetRequiredConfigurationValue(configuration, "Jwt:Audience");
        _key = GetRequiredConfigurationValue(configuration, "Jwt:Key");

        _accessTokenLifetimeMinutes =
            configuration.GetValue<int?>("Jwt:AccessTokenLifetimeMinutes") ?? 60;
    }

    public string GenerateAccessToken(User user)
    {
        var expiresAt = GetAccessTokenExpiresAtUtc();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public DateTime GetAccessTokenExpiresAtUtc()
    {
        return DateTime.UtcNow.AddMinutes(_accessTokenLifetimeMinutes);
    }

    private static string GetRequiredConfigurationValue(
        IConfiguration configuration,
        string configurationKey)
    {
        var value = configuration[configurationKey];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InfrastructureConfigurationException(configurationKey);
        }

        return value;
    }
}
