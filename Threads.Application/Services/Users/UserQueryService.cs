using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Search;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Common;

namespace Threads.Application.Services.Users;

public sealed class UserQueryService
{
    private const int MaxUsernameLength = 50;
    private const int MaximumSearchQueryLength = 100;

    private readonly IUserRepository _userRepository;
    private readonly IFollowRepository _followRepository;
    private readonly UserResponseFactory _responseFactory;
    private readonly HybridCache _cache;
    private readonly ILogger<UserQueryService> _logger;

    public UserQueryService(
        IUserRepository userRepository,
        IFollowRepository followRepository,
        UserResponseFactory responseFactory,
        HybridCache cache,
        ILogger<UserQueryService> logger)
    {
        _userRepository = userRepository;
        _followRepository = followRepository;
        _responseFactory = responseFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CursorPageResponse<UserShortResponse>> SearchAsync(
        string query,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var normalizedQuery = SearchQueryNormalizer.Normalize(
            query,
            MaximumSearchQueryLength,
            "User search query");

        if (normalizedQuery is null)
        {
            return new CursorPageResponse<UserShortResponse>
            {
                Items = [],
                HasMore = false,
                NextCursor = null
            };
        }

        var cursor = CursorCodec.DecodeText(pagination.Cursor);
        var users = await _userRepository.SearchAsync(
            normalizedQuery,
            pagination.Limit,
            cursor,
            cancellationToken);
        return CreateSearchPage(users, pagination.Limit);
    }

    public async Task<CursorPageResponse<UserShortResponse>> SearchAsync(
        SearchUsersRequest request,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        RequestValidator.Validate(request);
        var normalizedQuery = SearchQueryNormalizer.Normalize(
            request.Q,
            MaximumSearchQueryLength,
            "User search query");
        var people = SearchFilterNormalizer.NormalizeOption(
            request.People,
            "following",
            "People");
        var location = SearchFilterNormalizer.NormalizeOption(
            request.Location,
            "near",
            "Location");

        if (normalizedQuery is null && people is null && location is null)
        {
            return new CursorPageResponse<UserShortResponse>
            {
                Items = [],
                HasMore = false,
                NextCursor = null
            };
        }

        var cursor = CursorCodec.DecodeText(request.Cursor);
        var users = await _userRepository.SearchAsync(
            normalizedQuery,
            people,
            location,
            request.Limit,
            cursor,
            currentUserId,
            cancellationToken);
        return CreateSearchPage(users, request.Limit);
    }

    private CursorPageResponse<UserShortResponse> CreateSearchPage(
        IReadOnlyCollection<UserSummaryReadModel> users,
        int limit)
    {
        var hasMore = users.Count > limit;
        var pageUsers = users.Take(limit).ToList();

        return new CursorPageResponse<UserShortResponse>
        {
            Items = pageUsers
                .Select(_responseFactory.CreateShort)
                .ToList(),
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.EncodeText(pageUsers[^1].Username, pageUsers[^1].Id)
                : null
        };
    }

    public async Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cacheKey = UserProfileCache.GetProfileKey(id);
        var publicProfile = await _cache.GetOrCreateAsync<UserProfileReadModel?>(
            cacheKey,
            async token => await _userRepository.GetProfileByIdAsync(id, token),
            UserProfileCache.ProfileEntryOptions,
            cancellationToken: cancellationToken);

        if (publicProfile is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, _logger, cacheKey);
            return null;
        }

        var isOwner = currentUserId == publicProfile.Id;
        var isFollowedByCurrentUser = false;
        var followsCurrentUser = false;

        if (currentUserId.HasValue && !isOwner)
        {
            isFollowedByCurrentUser = await _followRepository.GetByFollowerAndFollowingAsync(
                currentUserId.Value,
                publicProfile.Id,
                cancellationToken) is not null;

            if (BirthDateVisibilityPolicy.RequiresOwnerFollowState(publicProfile))
            {
                followsCurrentUser = await _followRepository.GetByFollowerAndFollowingAsync(
                    publicProfile.Id,
                    currentUserId.Value,
                    cancellationToken) is not null;
            }
        }

        var canSeeBirthDate = isOwner || BirthDateVisibilityPolicy.CanSeeBirthDate(
            publicProfile,
            isFollowedByCurrentUser,
            followsCurrentUser);

        return _responseFactory.Create(publicProfile, isFollowedByCurrentUser, canSeeBirthDate);
    }

    public async Task<UserResponse?> GetMeAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var profile = await _userRepository.GetProfileByIdAsync(id, cancellationToken);

        return profile is null
            ? null
            : _responseFactory.CreateCurrent(user, profile);
    }

    public async Task<UserResponse?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();

        if (normalizedUsername.Length > MaxUsernameLength)
        {
            return null;
        }

        var cacheKey = UserProfileCache.GetUsernameKey(normalizedUsername);
        var userId = await _cache.GetOrCreateAsync<Guid?>(
            cacheKey,
            async token => await _userRepository.GetIdByUsernameAsync(normalizedUsername, token),
            UserProfileCache.UsernameEntryOptions,
            cancellationToken: cancellationToken);

        if (!userId.HasValue)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, _logger, cacheKey);
            return null;
        }

        var user = await GetByIdAsync(userId.Value, cancellationToken, currentUserId);

        if (user is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, _logger, cacheKey);
        }

        return user;
    }
}
