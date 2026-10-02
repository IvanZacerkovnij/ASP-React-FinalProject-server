using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Threads.Domain.Enums;
using Threads.Infrastructure.IntegrationTests.Infrastructure;
using Threads.Infrastructure.Services;

namespace Threads.Infrastructure.IntegrationTests.Security;

public sealed class JwtTokenServiceTests
{
    private const string Issuer = "threads-integration-tests";
    private const string Audience = "threads-client";
    private const string SigningKey = "integration-test-signing-key-with-at-least-32-bytes";

    [Fact]
    public void GenerateAccessToken_ProducesValidConfiguredClaimsAndExpiration()
    {
        var service = CreateService();
        var user = TestEntityFactory.CreateUser("admin-user", "admin@example.com");
        user.Role = UserRole.Moderator;
        var beforeGeneration = DateTimeOffset.UtcNow;

        var encodedToken = service.GenerateAccessToken(user);
        var afterGeneration = DateTimeOffset.UtcNow;
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(encodedToken);
        var principal = handler.ValidateToken(
            encodedToken,
            CreateValidationParameters(Audience),
            out var validatedToken);

        Assert.Equal(Issuer, token.Issuer);
        Assert.Contains(Audience, token.Audiences);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.UniqueName && claim.Value == user.Username);
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == user.Email);
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == UserRole.Moderator.ToString());
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.Jti && Guid.TryParse(claim.Value, out _));
        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(principal.IsInRole(UserRole.Moderator.ToString()));
        Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.InRange(
            token.ValidTo,
            beforeGeneration.UtcDateTime.AddMinutes(15).AddSeconds(-1),
            afterGeneration.UtcDateTime.AddMinutes(15).AddSeconds(1));
    }

    [Fact]
    public void GenerateAccessToken_WhenAudienceIsWrong_FailsValidation()
    {
        var service = CreateService();
        var encodedToken = service.GenerateAccessToken(TestEntityFactory.CreateUser());
        var handler = new JwtSecurityTokenHandler();

        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(
            encodedToken,
            CreateValidationParameters("wrong-audience"),
            out _));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsIndependentCryptographic32ByteValues()
    {
        var service = CreateService();

        var first = service.GenerateRefreshToken();
        var second = service.GenerateRefreshToken();

        Assert.NotEqual(first, second);
        Assert.Equal(32, Convert.FromBase64String(first).Length);
        Assert.Equal(32, Convert.FromBase64String(second).Length);
    }

    private static JwtTokenService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:Key"] = SigningKey,
                ["Jwt:AccessTokenLifetimeMinutes"] = "15"
            })
            .Build();

        return new JwtTokenService(configuration);
    }

    private static TokenValidationParameters CreateValidationParameters(string audience)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role
        };
    }
}
