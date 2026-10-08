using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Users;
using Threads.Application.UnitTests.TestSupport;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Services.Users;

public class UserProfileServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly TestHybridCache _cache = new();
    private readonly UserProfileService _service;

    public UserProfileServiceTests()
    {
        var profileImageManager = new ProfileImageManager(
            _objectStorageService,
            Substitute.For<ILogger<ProfileImageManager>>());
        var responseFactory = new UserResponseFactory(_objectStorageService, _mapper);
        _mapper
            .Map<UserResponse>(Arg.Any<User>())
            .Returns(callInfo => MapUser(callInfo.Arg<User>()));
        _objectStorageService
            .GetReadUrl(Arg.Any<string>())
            .Returns(callInfo => $"https://cdn.example/{callInfo.Arg<string>()}");

        _service = new UserProfileService(
            _userRepository,
            profileImageManager,
            responseFactory,
            _cache,
            Substitute.For<ILogger<UserProfileService>>());
    }

    [Fact]
    public async Task UpdateAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        var result = await _service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateUserRequest { DisplayName = "New Name" });

        Assert.Null(result);
        await _userRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenRequestIsValid_UpdatesProfileReplacesImagesAndInvalidatesCache()
    {
        var user = CreateUser();
        user.AvatarObjectKey = "old-avatar";
        user.BannerObjectKey = "old-banner";
        using var avatarContent = new MemoryStream([1, 2]);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var avatar = CreateFile(avatarContent, "avatar.PNG", "image/png");
        var request = new UpdateUserRequest
        {
            DisplayName = "  New Name  ",
            Bio = "  ",
            DateOfBirth = new DateOnly(2000, 1, 2),
            Location = new LocationRequest
            {
                Id = " place-id ",
                Name = " Kyiv ",
                Country = " UA ",
                Latitude = 50.45,
                Longitude = 30.52
            },
            RemoveBanner = true
        };

        _userRepository
            .GetByIdAsync(user.Id, cancellationToken)
            .Returns(user);
        _userRepository
            .GetProfileByIdAsync(user.Id, cancellationToken)
            .Returns(_ => CreateProfile(user));

        var result = await _service.UpdateAsync(
            user.Id,
            request,
            avatar,
            cancellationToken: cancellationToken);

        Assert.NotNull(result);
        Assert.Equal("New Name", user.DisplayName);
        Assert.Null(user.Bio);
        Assert.Equal(new DateOnly(2000, 1, 2), user.DateOfBirth);
        Assert.Equal("Kyiv", user.Location);
        Assert.Equal("place-id", user.LocationPlaceId);
        Assert.Null(user.BannerObjectKey);
        Assert.StartsWith($"users/{user.Id}/profile/avatar-", user.AvatarObjectKey);
        Assert.Equal($"https://cdn.example/{user.AvatarObjectKey}", result.AvatarUrl);
        Assert.Null(result.BannerUrl);
        await _userRepository.Received(1).UpdateAsync(user, cancellationToken);
        await _objectStorageService.Received(1).DeleteAsync("old-avatar", cancellationToken);
        await _objectStorageService.Received(1).DeleteAsync("old-banner", cancellationToken);
        Assert.Contains($"users:profile:v1:{user.Id:N}", _cache.RemovedKeys);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_WhenProfileDataIsInvalid_ThrowsWithoutPersisting(bool futureBirthDate)
    {
        var user = CreateUser();
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        var request = futureBirthDate
            ? new UpdateUserRequest
            {
                DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1)
            }
            : new UpdateUserRequest
            {
                Location = new LocationRequest { Name = " " }
            };

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.UpdateAsync(user.Id, request));

        await _userRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
        await _objectStorageService.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenPersistenceFails_DeletesNewUploadsAndRethrows()
    {
        var user = CreateUser();
        user.AvatarObjectKey = "old-avatar";
        using var avatarContent = new MemoryStream([1]);
        var avatar = CreateFile(avatarContent, "avatar.jpg", "image/jpeg");
        string? uploadedObjectKey = null;
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _objectStorageService
            .UploadAsync(
                avatarContent,
                Arg.Do<string>(key => uploadedObjectKey = key),
                avatar.ContentType,
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _userRepository
            .UpdateAsync(user, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("database failed")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateAsync(user.Id, new UpdateUserRequest(), avatar));

        Assert.Equal("database failed", exception.Message);
        Assert.NotNull(uploadedObjectKey);
        await _objectStorageService.Received(1).DeleteAsync(
            uploadedObjectKey,
            Arg.Any<CancellationToken>());
        await _objectStorageService.DidNotReceive().DeleteAsync(
            "old-avatar",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenRemovalFlagsAreSet_ClearsOptionalProfileData()
    {
        var user = CreateUser();
        user.DateOfBirth = new DateOnly(2000, 1, 1);
        user.Location = "Kyiv";
        user.LocationPlaceId = "place-id";
        user.LocationCountry = "UA";
        user.LocationLatitude = 50.45;
        user.LocationLongitude = 30.52;
        user.AvatarObjectKey = "avatar-key";
        user.BannerObjectKey = "banner-key";
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository
            .GetProfileByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(_ => CreateProfile(user));

        await _service.UpdateAsync(user.Id, new UpdateUserRequest
        {
            RemoveDateOfBirth = true,
            RemoveLocation = true,
            RemoveAvatar = true,
            RemoveBanner = true
        });

        Assert.Null(user.DateOfBirth);
        Assert.Null(user.Location);
        Assert.Null(user.LocationPlaceId);
        Assert.Null(user.LocationCountry);
        Assert.Null(user.LocationLatitude);
        Assert.Null(user.LocationLongitude);
        Assert.Null(user.AvatarObjectKey);
        Assert.Null(user.BannerObjectKey);
        await _objectStorageService.Received(1).DeleteAsync(
            "avatar-key",
            Arg.Any<CancellationToken>());
        await _objectStorageService.Received(1).DeleteAsync(
            "banner-key",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenVisibilityIsProvided_PersistsAndReturnsVisibility()
    {
        var user = CreateUser();
        SetupUser(user);

        var result = await _service.UpdateAsync(user.Id, new UpdateUserRequest
        {
            BirthDateVisibility = VisibilityLevel.Followers,
            BirthYearVisibility = VisibilityLevel.Public
        });

        Assert.Equal(VisibilityLevel.Followers, user.BirthDateVisibility);
        Assert.Equal(VisibilityLevel.Public, user.BirthYearVisibility);
        Assert.NotNull(result);
        Assert.Equal(VisibilityLevel.Followers, result.BirthDateVisibility);
        Assert.Equal(VisibilityLevel.Public, result.BirthYearVisibility);
    }

    [Fact]
    public async Task UpdateAsync_WhenVisibilityIsOmitted_KeepsExistingVisibility()
    {
        var user = CreateUser();
        user.BirthDateVisibility = VisibilityLevel.Mutual;
        user.BirthYearVisibility = VisibilityLevel.Following;
        SetupUser(user);

        await _service.UpdateAsync(user.Id, new UpdateUserRequest { DisplayName = "New Name" });

        Assert.Equal(VisibilityLevel.Mutual, user.BirthDateVisibility);
        Assert.Equal(VisibilityLevel.Following, user.BirthYearVisibility);
    }

    [Fact]
    public void NewUser_DefaultsBirthDateVisibilityToOnlyMe()
    {
        var user = CreateUser();

        Assert.Equal(VisibilityLevel.OnlyMe, user.BirthDateVisibility);
        Assert.Equal(VisibilityLevel.OnlyMe, user.BirthYearVisibility);
    }

    private void SetupUser(User user)
    {
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository
            .GetProfileByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(_ => CreateProfile(user));
    }

    private static UserFileUploadRequest CreateFile(
        Stream content,
        string fileName,
        string contentType)
    {
        return new UserFileUploadRequest
        {
            Content = content,
            FileName = fileName,
            ContentType = contentType,
            SizeInBytes = content.Length
        };
    }

    private static User CreateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Username = "testuser",
            PasswordHash = "password-hash",
            DisplayName = "Old Name",
            Bio = "Old Bio",
            IsActive = true,
            IsVerified = true
        };
    }

    private static UserProfileReadModel CreateProfile(User user)
    {
        return new UserProfileReadModel
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            DateOfBirth = user.DateOfBirth,
            BirthDateVisibility = user.BirthDateVisibility,
            BirthYearVisibility = user.BirthYearVisibility,
            LocationPlaceId = user.LocationPlaceId,
            LocationName = user.Location,
            LocationCountry = user.LocationCountry,
            LocationLatitude = user.LocationLatitude,
            LocationLongitude = user.LocationLongitude,
            AvatarObjectKey = user.AvatarObjectKey,
            BannerObjectKey = user.BannerObjectKey,
            FollowersCount = user.FollowerRelations.Count,
            FollowingCount = user.FollowingRelations.Count,
            PostsCount = user.Posts.Count,
            IsVerified = user.IsVerified,
            CreatedAt = user.CreatedAt
        };
    }

    private static UserResponse MapUser(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            BirthDate = user.DateOfBirth,
            IsVerified = user.IsVerified,
            CreatedAt = user.CreatedAt
        };
    }
}
