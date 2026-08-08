using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Threads.Application.Interfaces.Security;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Services;

public class JwtTokenService : ITokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;
    private readonly int _accessTokenLifetimeMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");

        _issuer = jwtSection["Issuer"] ??
            throw new InvalidOperationException("Jwt:Issuer is not configured.");

        _audience = jwtSection["Audience"] ??
            throw new InvalidOperationException("Jwt:Audience is not configured.");

        _key = jwtSection["Key"] ??
            throw new InvalidOperationException("Jwt:Key is not configured.");

        _accessTokenLifetimeMinutes =
            jwtSection.GetValue<int?>("AccessTokenLifetimeMinutes") ?? 60;
    }

    public string GenerateAccessToken(User user)
    {
        var expiresAt = GetAccessTokenExpiresAtUtc();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
}
