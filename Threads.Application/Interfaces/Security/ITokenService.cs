using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Security;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    DateTime GetAccessTokenExpiresAtUtc();
}
