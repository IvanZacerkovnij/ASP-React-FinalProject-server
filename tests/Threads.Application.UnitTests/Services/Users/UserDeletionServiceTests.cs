using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Users;
using Threads.Application.UnitTests.TestSupport;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Users;

public class UserDeletionServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly TestHybridCache _cache = new();
    private readonly UserDeletionService _service;

    public UserDeletionServiceTests()
    {
        var profileImageManager = new ProfileImageManager(
            _objectStorageService,
            Substitute.For<ILogger<ProfileImageManager>>());
        _service = new UserDeletionService(
            _userRepository,
            _mediaRepository,
            profileImageManager,
            _cache,
            Substitute.For<ILogger<UserDeletionService>>());
    }

    [Fact]
    public async Task DeleteAsync_WhenUserDoesNotExist_ReturnsFalse()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid());

        Assert.False(result);
        await _mediaRepository.DidNotReceive().GetStorageKeysByUploaderIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenUserExists_DeletesDataStorageAndAffectedCaches()
    {
        var user = CreateUser();
        var followingId = Guid.NewGuid();
        var followerId = Guid.NewGuid();
        var post = new Post { Id = Guid.NewGuid(), AuthorId = user.Id };
        user.AvatarObjectKey = "avatar-key";
        user.BannerObjectKey = "banner-key";
        user.FollowingRelations.Add(new Follow
        {
            FollowerId = user.Id,
            FollowingId = followingId
        });
        user.FollowerRelations.Add(new Follow
        {
            FollowerId = followerId,
            FollowingId = user.Id
        });
        user.Posts.Add(post);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _userRepository.GetWithRelationsByIdAsync(user.Id, cancellationToken).Returns(user);
        _mediaRepository
            .GetStorageKeysByUploaderIdAsync(user.Id, cancellationToken)
            .Returns(["media-key", "avatar-key"]);

        var result = await _service.DeleteAsync(user.Id, cancellationToken);

        Assert.True(result);
        await _userRepository.Received(1).DeleteAsync(user, cancellationToken);
        await _objectStorageService.Received(1).DeleteAsync("media-key", cancellationToken);
        await _objectStorageService.Received(1).DeleteAsync("avatar-key", cancellationToken);
        await _objectStorageService.Received(1).DeleteAsync("banner-key", cancellationToken);
        Assert.Contains($"users:profile:v1:{user.Id:N}", _cache.RemovedKeys);
        Assert.Contains($"users:profile:v1:{followingId:N}", _cache.RemovedKeys);
        Assert.Contains($"users:profile:v1:{followerId:N}", _cache.RemovedKeys);
        Assert.Contains($"users:username:v1:{user.Username}", _cache.RemovedKeys);
        Assert.Contains($"posts:core:v1:{post.Id:N}", _cache.RemovedKeys);
    }

    private static User CreateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Username = "testuser",
            PasswordHash = "password-hash"
        };
    }
}
