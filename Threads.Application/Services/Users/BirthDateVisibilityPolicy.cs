using Threads.Application.DTOs.Users;
using Threads.Domain.Enums;

namespace Threads.Application.Services.Users;

public static class BirthDateVisibilityPolicy
{
    public static bool CanSeeBirthDate(
        UserProfileReadModel profile,
        bool viewerFollowsOwner,
        bool ownerFollowsViewer)
    {
        return CanSee(profile.BirthDateVisibility, viewerFollowsOwner, ownerFollowsViewer) &&
            CanSee(profile.BirthYearVisibility, viewerFollowsOwner, ownerFollowsViewer);
    }

    public static bool CanSee(
        VisibilityLevel level,
        bool viewerFollowsOwner,
        bool ownerFollowsViewer)
    {
        return level switch
        {
            VisibilityLevel.Public => true,
            VisibilityLevel.Followers => viewerFollowsOwner,
            VisibilityLevel.Following => ownerFollowsViewer,
            VisibilityLevel.Mutual => viewerFollowsOwner && ownerFollowsViewer,
            _ => false
        };
    }

    public static bool RequiresOwnerFollowState(UserProfileReadModel profile)
    {
        return profile.DateOfBirth.HasValue &&
            (DependsOnOwnerFollowing(profile.BirthDateVisibility) ||
                DependsOnOwnerFollowing(profile.BirthYearVisibility));
    }

    private static bool DependsOnOwnerFollowing(VisibilityLevel level)
    {
        return level is VisibilityLevel.Following or VisibilityLevel.Mutual;
    }
}
