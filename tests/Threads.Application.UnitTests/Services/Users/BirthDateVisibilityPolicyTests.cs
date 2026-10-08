using Threads.Application.DTOs.Users;
using Threads.Application.Services.Users;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Services.Users;

public class BirthDateVisibilityPolicyTests
{
    [Theory]
    [InlineData(VisibilityLevel.Public, false, false, true)]
    [InlineData(VisibilityLevel.Followers, false, false, false)]
    [InlineData(VisibilityLevel.Followers, true, false, true)]
    [InlineData(VisibilityLevel.Followers, false, true, false)]
    [InlineData(VisibilityLevel.Following, false, false, false)]
    [InlineData(VisibilityLevel.Following, true, false, false)]
    [InlineData(VisibilityLevel.Following, false, true, true)]
    [InlineData(VisibilityLevel.Mutual, true, false, false)]
    [InlineData(VisibilityLevel.Mutual, false, true, false)]
    [InlineData(VisibilityLevel.Mutual, true, true, true)]
    [InlineData(VisibilityLevel.OnlyMe, true, true, false)]
    public void CanSee_ReturnsExpectedResultForRelationship(
        VisibilityLevel level,
        bool viewerFollowsOwner,
        bool ownerFollowsViewer,
        bool expected)
    {
        var result = BirthDateVisibilityPolicy.CanSee(level, viewerFollowsOwner, ownerFollowsViewer);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanSeeBirthDate_WhenYearIsHidden_ReturnsFalse()
    {
        var profile = CreateProfile(VisibilityLevel.Public, VisibilityLevel.OnlyMe);

        var result = BirthDateVisibilityPolicy.CanSeeBirthDate(
            profile,
            viewerFollowsOwner: true,
            ownerFollowsViewer: true);

        Assert.False(result);
    }

    [Fact]
    public void CanSeeBirthDate_WhenDateAndYearAreVisible_ReturnsTrue()
    {
        var profile = CreateProfile(VisibilityLevel.Followers, VisibilityLevel.Public);

        var result = BirthDateVisibilityPolicy.CanSeeBirthDate(
            profile,
            viewerFollowsOwner: true,
            ownerFollowsViewer: false);

        Assert.True(result);
    }

    [Theory]
    [InlineData(VisibilityLevel.Following, VisibilityLevel.Public, true)]
    [InlineData(VisibilityLevel.Public, VisibilityLevel.Mutual, true)]
    [InlineData(VisibilityLevel.Followers, VisibilityLevel.OnlyMe, false)]
    public void RequiresOwnerFollowState_DependsOnVisibilityLevels(
        VisibilityLevel dateVisibility,
        VisibilityLevel yearVisibility,
        bool expected)
    {
        var profile = CreateProfile(dateVisibility, yearVisibility);

        var result = BirthDateVisibilityPolicy.RequiresOwnerFollowState(profile);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void RequiresOwnerFollowState_WhenBirthDateIsMissing_ReturnsFalse()
    {
        var profile = new UserProfileReadModel
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            BirthDateVisibility = VisibilityLevel.Mutual,
            BirthYearVisibility = VisibilityLevel.Mutual
        };

        var result = BirthDateVisibilityPolicy.RequiresOwnerFollowState(profile);

        Assert.False(result);
    }

    private static UserProfileReadModel CreateProfile(
        VisibilityLevel dateVisibility,
        VisibilityLevel yearVisibility)
    {
        return new UserProfileReadModel
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            DateOfBirth = new DateOnly(2000, 1, 2),
            BirthDateVisibility = dateVisibility,
            BirthYearVisibility = yearVisibility
        };
    }
}
