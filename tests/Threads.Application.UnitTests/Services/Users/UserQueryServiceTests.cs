using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Users;
using Threads.Application.UnitTests.TestSupport;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Users;

public class UserQueryServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly TestHybridCache _cache = new();
    private readonly UserQueryService _service;

    public UserQueryServiceTests()
    {
        _objectStorageService
            .GetReadUrl(Arg.Any<string>())
            .Returns(callInfo => $"https://cdn.example/{callInfo.Arg<string>()}");
        _mapper
            .Map<UserResponse>(Arg.Any<User>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<User>();
                return new UserResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    IsVerified = user.IsVerified,
                    CreatedAt = user.CreatedAt
                };
            });
        var responseFactory = new UserResponseFactory(_objectStorageService, _mapper);
        _service = new UserQueryService(
            _userRepository,
            _followRepository,
            responseFactory,
            _cache,
            Substitute.For<ILogger<UserQueryService>>());
    }

    [Fact]
    public async Task SearchAsync_WhenQueryIsBlank_ReturnsEmptyPageWithoutRepositoryCall()
    {
        var result = await _service.SearchAsync("  ", new CursorPageRequest());

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
        await _userRepository.DidNotReceive().SearchAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<TextCursorPosition?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WhenResultsExceedLimit_NormalizesMapsAndPaginates()
    {
        var users = new[]
        {
            CreateSummary("alpha"),
            CreateSummary("beta"),
            CreateSummary("gamma")
        };
        users[0] = new UserSummaryReadModel
        {
            Id = users[0].Id,
            Username = users[0].Username,
            AvatarObjectKey = "avatars/alpha.jpg"
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _userRepository
            .SearchAsync("alp", 2, null, cancellationToken)
            .Returns(users);

        var result = await _service.SearchAsync(
            "  alp  ",
            new CursorPageRequest { Limit = 2 },
            cancellationToken);

        Assert.Equal(2, result.Items.Count);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
        Assert.Equal("https://cdn.example/avatars/alpha.jpg", result.Items.First().AvatarUrl);
    }

    [Fact]
    public async Task SearchAsync_WhenCursorIsInvalid_ThrowsRequestValidationException()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.SearchAsync(
                "query",
                new CursorPageRequest { Cursor = "not-a-cursor" }));

        await _userRepository.DidNotReceive().SearchAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<TextCursorPosition?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenProfileDoesNotExist_ReturnsNullAndRemovesCacheEntry()
    {
        var userId = Guid.NewGuid();

        var result = await _service.GetByIdAsync(userId);

        Assert.Null(result);
        Assert.Contains($"users:profile:v1:{userId:N}", _cache.RemovedKeys);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProfileExists_MapsFollowStateAndPublicFields()
    {
        var currentUserId = Guid.NewGuid();
        var profile = CreateProfile();
        profile = new UserProfileReadModel
        {
            Id = profile.Id,
            Username = profile.Username,
            AvatarObjectKey = "avatars/profile.jpg",
            FollowersCount = 3,
            CreatedAt = profile.CreatedAt
        };
        _userRepository
            .GetProfileByIdAsync(profile.Id, Arg.Any<CancellationToken>())
            .Returns(profile);
        _followRepository
            .GetByFollowerAndFollowingAsync(
                currentUserId,
                profile.Id,
                Arg.Any<CancellationToken>())
            .Returns(new Follow { FollowerId = currentUserId, FollowingId = profile.Id });

        var result = await _service.GetByIdAsync(profile.Id, currentUserId: currentUserId);

        Assert.NotNull(result);
        Assert.Null(result.Email);
        Assert.True(result.IsFollowedByCurrentUser);
        Assert.Equal(3, result.FollowersCount);
        Assert.Equal("https://cdn.example/avatars/profile.jpg", result.AvatarUrl);
    }

    [Fact]
    public async Task GetMeAsync_WhenUserExists_ReturnsPrivateProfile()
    {
        var user = CreateUser();
        var profile = CreateProfile(user);
        _userRepository
            .GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository
            .GetProfileByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _service.GetMeAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.Username, result.Username);
        Assert.Equal(profile.FollowersCount, result.FollowersCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz")]
    public async Task GetByUsernameAsync_WhenUsernameIsInvalid_ReturnsNull(string username)
    {
        var result = await _service.GetByUsernameAsync(username);

        Assert.Null(result);
        await _userRepository.DidNotReceive().GetIdByUsernameAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenUsernameExists_NormalizesAndReturnsProfile()
    {
        var profile = CreateProfile();
        _userRepository
            .GetIdByUsernameAsync("testuser", Arg.Any<CancellationToken>())
            .Returns(profile.Id);
        _userRepository
            .GetProfileByIdAsync(profile.Id, Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _service.GetByUsernameAsync("  TestUser  ");

        Assert.NotNull(result);
        Assert.Equal(profile.Id, result.Id);
        Assert.Equal(profile.Username, result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_WhenUsernameDoesNotExist_ReturnsNullAndInvalidatesLookup()
    {
        var result = await _service.GetByUsernameAsync("missing");

        Assert.Null(result);
        Assert.Contains("users:username:v1:missing", _cache.RemovedKeys);
        await _userRepository.Received(1).GetIdByUsernameAsync(
            "missing",
            Arg.Any<CancellationToken>());
    }

    private static UserSummaryReadModel CreateSummary(string username)
    {
        return new UserSummaryReadModel
        {
            Id = Guid.NewGuid(),
            Username = username
        };
    }

    private static UserProfileReadModel CreateProfile()
    {
        return new UserProfileReadModel
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static UserProfileReadModel CreateProfile(User user)
    {
        return new UserProfileReadModel
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            FollowersCount = 3,
            FollowingCount = 4,
            PostsCount = 5,
            IsVerified = user.IsVerified,
            CreatedAt = user.CreatedAt
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
            IsVerified = true
        };
    }
}
