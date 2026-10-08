using Threads.Application.Exceptions;

namespace Threads.Application.Services.Admin;

internal static class AdminActionPolicy
{
    public static void EnsureNotSelf(Guid targetUserId, Guid adminId)
    {
        if (targetUserId == adminId)
        {
            throw new ForbiddenException("Administrators cannot modify their own account.");
        }
    }
}
