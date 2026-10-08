using System.Security.Claims;
using Threads.Api.Exceptions;

namespace Threads.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetCurrentUserId(this ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }

    public static Guid GetRequiredCurrentUserId(this ClaimsPrincipal user)
    {
        return user.GetCurrentUserId()
            ?? throw new InvalidUserClaimsException();
    }
}
