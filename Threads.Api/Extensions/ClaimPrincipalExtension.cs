using System.Security.Claims;

namespace Threads.Api.Extensions;

public static class ClaimPrincipalExtension
{
    public static Guid? GetCurrentUserId(this ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        
        return Guid.TryParse(userId, out var parsedUserId) 
            ? parsedUserId 
            : null;
    }
}