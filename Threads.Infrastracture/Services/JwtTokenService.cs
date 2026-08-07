using System.Security.Cryptography;
using System.Text;
using Threads.Application.Interfaces.Security;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Services;

public class JwtTokenService : ITokenService
{
    public string GenerateAccessToken(User user)
    {
        var rawValue = $"{user.Id}:{user.Username}:{DateTime.UtcNow.Ticks}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(rawValue));
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public DateTime GetAccessTokenExpiresAtUtc()
    {
        return DateTime.UtcNow.AddHours(1);
    }
}
