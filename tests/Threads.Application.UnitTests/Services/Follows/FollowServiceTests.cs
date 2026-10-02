using AutoMapper;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Follows;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Follows;

public class FollowServiceTests
{
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();
    private readonly FollowService _service;

    public FollowServiceTests()
    {
        _mapper
            .Map<UserShortResponse>(Arg.Any<User>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<User>();
                return new UserShortResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    IsVerified = user.IsVerified
                };
            });
        _objectStorageService
            .GetReadUrl(Arg.Any<string>())
            .Returns(callInfo => $"https://cdn.example/{callInfo.Arg<string>()}");

        _service = new FollowService(
            _followRepository,
            _userRepository,
            _objectStorageService,
            _mapper,
            _cache,
            Substitute.For<ILogger<FollowService>>());
    }

    [Fact]
    public async Task AddFollowAsync_WhenFollowingSelf_ReturnsFalseWithoutRepositoryCalls()
    {
        var userId = Guid.NewGuid();

        var result = await _service.AddFollowAsync(userId, userId);

        Assert.False(result);
        await _userRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _followRepository.DidNotReceive().TryAddAsync(
            Arg.Any<Follow>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFollowAsync_WhenEitherUserDoesNotExist_ReturnsFalse()
    {
        var follower = CreateUser();
        var followingId = Guid.NewGuid();
        _userRepository
            .GetByIdAsync(follower.Id, Arg.Any<CancellationToken>())
            .Returns(follower);

        var result = await _service.AddFollowAsync(follower.Id, followingId);

        Assert.False(result);
        await _followRepository.DidNotReceive().GetByFollowerAndFollowingAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFollowAsync_WhenFollowAlreadyExists_ReturnsFalse()
    {
        var follower = CreateUser();
        var following = CreateUser();
        ConfigureUsers(follower, following);
        _followRepository
            .GetByFollowerAndFollowingAsync(
                follower.Id,
                following.Id,
                Arg.Any<CancellationToken>())
            .Returns(new Follow { FollowerId = follower.Id, FollowingId = following.Id });

        var result = await _service.AddFollowAsync(follower.Id, following.Id);

        Assert.False(result);
        await _followRepository.DidNotReceive().TryAddAsync(
            Arg.Any<Follow>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFollowAsync_WhenUsersExist_AddsFollowAndInvalidatesBothProfiles()
    {
        var follower = CreateUser();
        var following = CreateUser();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        Follow? addedFollow = null;
        ConfigureUsers(follower, following);
        _followRepository
            .TryAddAsync(
                Arg.Do<Follow>(follow => addedFollow = follow),
                cancellationToken)
            .Returns(true);

        var result = await _service.AddFollowAsync(
            follower.Id,
            following.Id,
            cancellationToken);

        Assert.True(result);
        Assert.NotNull(addedFollow);
        Assert.Equal(follower.Id, addedFollow.FollowerId);
        Assert.Equal(following.Id, addedFollow.FollowingId);
        await _cache.Received(1).RemoveAsync(
            $"users:profile:v1:{follower.Id:N}",
            Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(
            $"users:profile:v1:{following.Id:N}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFollowAsync_WhenConcurrentInsertFails_ReturnsFalseWithoutInvalidatingCache()
    {
        var follower = CreateUser();
        var following = CreateUser();
        ConfigureUsers(follower, following);
        _followRepository
            .TryAddAsync(Arg.Any<Follow>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _service.AddFollowAsync(follower.Id, following.Id);

        Assert.False(result);
        await _cache.DidNotReceive().RemoveAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFollowAsync_WhenFollowDoesNotExist_ReturnsFalse()
    {
        var result = await _service.RemoveFollowAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        await _followRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Follow>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFollowAsync_WhenFollowExists_DeletesItAndInvalidatesProfiles()
    {
        var follow = new Follow
        {
            FollowerId = Guid.NewGuid(),
            FollowingId = Guid.NewGuid()
        };
        _followRepository
            .GetByFollowerAndFollowingAsync(
                follow.FollowerId,
                follow.FollowingId,
                Arg.Any<CancellationToken>())
            .Returns(follow);

        var result = await _service.RemoveFollowAsync(follow.FollowerId, follow.FollowingId);

        Assert.True(result);
        await _followRepository.Received(1).DeleteAsync(follow, Arg.Any<CancellationToken>());
        await _cache.Received(2).RemoveAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFollowerAsync_WhenActingForAnotherUser_ThrowsForbiddenException()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.RemoveFollowerAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        await _userRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFollowerAsync_WhenFollowDoesNotExist_ThrowsNotFoundException()
    {
        var user = CreateUser();
        var follower = CreateUser();
        ConfigureUsers(user, follower);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RemoveFollowerAsync(user.Id, user.Id, follower.Id));

        Assert.Equal("Follow was not found.", exception.Message);
    }

    [Fact]
    public async Task RemoveFollowerAsync_WhenRequestIsValid_RemovesFollowerRelationship()
    {
        var user = CreateUser();
        var follower = CreateUser();
        var follow = new Follow
        {
            FollowerId = follower.Id,
            FollowingId = user.Id
        };
        ConfigureUsers(user, follower);
        _followRepository
            .GetByFollowerAndFollowingAsync(
                follower.Id,
                user.Id,
                Arg.Any<CancellationToken>())
            .Returns(follow);

        await _service.RemoveFollowerAsync(user.Id, user.Id, follower.Id);

        await _followRepository.Received(1).DeleteAsync(
            follow,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFollowersAsync_WhenThereIsAnotherPage_MapsUsersAndCreatesCursor()
    {
        var userId = Guid.NewGuid();
        var firstUser = CreateUser("first");
        firstUser.AvatarObjectKey = "avatars/first.jpg";
        firstUser.Location = "Kyiv";
        firstUser.LocationCountry = "UA";
        firstUser.LocationLatitude = 50.45;
        firstUser.LocationLongitude = 30.52;
        var secondUser = CreateUser("second");
        var follows = new[]
        {
            CreateFollow(firstUser, userId, DateTimeOffset.UtcNow),
            CreateFollow(secondUser, userId, DateTimeOffset.UtcNow.AddMinutes(-1))
        };
        _followRepository
            .GetFollowersAsync(
                userId,
                1,
                null,
                Arg.Any<CancellationToken>())
            .Returns(follows);

        var result = await _service.GetFollowersAsync(
            userId,
            new CursorPageRequest { Limit = 1 });

        var item = Assert.Single(result.Items);
        Assert.Equal(firstUser.Id, item.Id);
        Assert.Equal("https://cdn.example/avatars/first.jpg", item.AvatarUrl);
        Assert.NotNull(item.Location);
        Assert.Equal("Kyiv", item.Location.Name);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task GetFollowingAsync_MapsFollowingUserRatherThanFollower()
    {
        var userId = Guid.NewGuid();
        var following = CreateUser("following");
        var follow = new Follow
        {
            FollowerId = userId,
            FollowingId = following.Id,
            Following = following,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _followRepository
            .GetFollowingAsync(userId, 10, null, Arg.Any<CancellationToken>())
            .Returns([follow]);

        var result = await _service.GetFollowingAsync(
            userId,
            new CursorPageRequest { Limit = 10 });

        Assert.Equal(following.Id, Assert.Single(result.Items).Id);
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
    }

    private void ConfigureUsers(User first, User second)
    {
        _userRepository
            .GetByIdAsync(first.Id, Arg.Any<CancellationToken>())
            .Returns(first);
        _userRepository
            .GetByIdAsync(second.Id, Arg.Any<CancellationToken>())
            .Returns(second);
    }

    private static Follow CreateFollow(User follower, Guid followingId, DateTimeOffset createdAt)
    {
        return new Follow
        {
            Id = Guid.NewGuid(),
            FollowerId = follower.Id,
            Follower = follower,
            FollowingId = followingId,
            CreatedAt = createdAt
        };
    }

    private static User CreateUser(string? username = null)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            Username = username ?? $"user-{Guid.NewGuid():N}",
            PasswordHash = "password-hash",
            IsActive = true,
            IsVerified = true
        };
    }
}
