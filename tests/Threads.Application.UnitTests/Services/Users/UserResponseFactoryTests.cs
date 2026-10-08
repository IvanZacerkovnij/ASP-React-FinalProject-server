using AutoMapper;
using NSubstitute;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Users;

public class UserResponseFactoryTests
{
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly UserResponseFactory _factory;

    public UserResponseFactoryTests()
    {
        _objectStorageService
            .GetReadUrl(Arg.Any<string>())
            .Returns(callInfo => $"https://cdn.example/{callInfo.Arg<string>()}");
        _factory = new UserResponseFactory(_objectStorageService, _mapper);
    }

    [Fact]
    public void CreateShort_FromReadModel_MapsLocationAndAvatarUrl()
    {
        var user = new UserSummaryReadModel
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            DisplayName = "Test User",
            LocationName = "Kyiv",
            LocationCountry = null,
            LocationLatitude = 50.45,
            LocationLongitude = 30.52,
            AvatarObjectKey = "avatars/user.jpg",
            IsVerified = true
        };

        var result = _factory.CreateShort(user);

        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Username, result.Username);
        Assert.Equal("https://cdn.example/avatars/user.jpg", result.AvatarUrl);
        Assert.NotNull(result.Location);
        Assert.Equal("Kyiv", result.Location.Id);
        Assert.Equal(string.Empty, result.Location.Country);
    }

    [Fact]
    public void Create_FromEntity_MapsPrivateFieldsMediaAndFollowState()
    {
        var currentUserId = Guid.NewGuid();
        var user = CreateUser();
        user.LocationPlaceId = "place-id";
        user.Location = "Lviv";
        user.LocationCountry = "UA";
        user.AvatarObjectKey = "avatars/user.jpg";
        user.BannerObjectKey = "banners/user.jpg";
        user.FollowerRelations.Add(new Follow
        {
            FollowerId = currentUserId,
            FollowingId = user.Id
        });
        _mapper.Map<UserResponse>(user).Returns(new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            FollowersCount = 1,
            FollowingCount = 2,
            PostsCount = 3,
            IsVerified = true,
            CreatedAt = user.CreatedAt
        });

        var result = _factory.Create(user, currentUserId);

        Assert.Equal(user.Email, result.Email);
        Assert.True(result.IsFollowedByCurrentUser);
        Assert.Equal("https://cdn.example/avatars/user.jpg", result.AvatarUrl);
        Assert.Equal("https://cdn.example/banners/user.jpg", result.BannerUrl);
        Assert.Equal("place-id", result.Location?.Id);
    }

    [Fact]
    public void Create_FromPublicProfile_HidesEmailAndPreservesCounts()
    {
        var profile = new UserProfileReadModel
        {
            Id = Guid.NewGuid(),
            Username = "public-user",
            FollowersCount = 4,
            FollowingCount = 5,
            PostsCount = 6,
            IsVerified = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = _factory.Create(profile, isFollowedByCurrentUser: true, canSeeBirthDate: true);

        Assert.Null(result.Email);
        Assert.Equal(4, result.FollowersCount);
        Assert.Equal(5, result.FollowingCount);
        Assert.Equal(6, result.PostsCount);
        Assert.True(result.IsFollowedByCurrentUser);
        Assert.Null(result.Location);
    }

    [Fact]
    public void CreateCurrent_CombinesPrivateIdentityWithProjectedProfileCounts()
    {
        var user = CreateUser();
        var profile = new UserProfileReadModel
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            FollowersCount = 4,
            FollowingCount = 5,
            PostsCount = 6,
            IsVerified = user.IsVerified,
            CreatedAt = user.CreatedAt
        };

        var result = _factory.CreateCurrent(user, profile);

        Assert.Equal(user.Email, result.Email);
        Assert.Equal(4, result.FollowersCount);
        Assert.Equal(5, result.FollowingCount);
        Assert.Equal(6, result.PostsCount);
        Assert.False(result.IsFollowedByCurrentUser);
    }

    private static User CreateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Username = "testuser",
            PasswordHash = "password-hash",
            DisplayName = "Test User",
            Bio = "Bio",
            IsVerified = true
        };
    }
}
