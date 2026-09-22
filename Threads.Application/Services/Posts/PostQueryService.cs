using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Common;

namespace Threads.Application.Services.Posts;

public sealed class PostQueryService
{
    private const int FeedSize = 10;
    private const int MaximumSearchQueryLength = 100;

    private readonly IPostRepository _postRepository;
    private readonly IUserService _userService;
    private readonly PostResponseFactory _responseFactory;
    private readonly HybridCache _cache;
    private readonly ILogger<PostQueryService> _logger;

    public PostQueryService(
        IPostRepository postRepository,
        IUserService userService,
        PostResponseFactory responseFactory,
        HybridCache cache,
        ILogger<PostQueryService> logger)
    {
        _postRepository = postRepository;
        _userService = userService;
        _responseFactory = responseFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetRandomAsync(
            FeedSize,
            currentUserId,
            cancellationToken);

        return posts
            .Select(_responseFactory.Create)
            .ToList();
    }

    public async Task<CursorPageResponse<PostResponse>> GetByAuthorIdAsync(
        Guid authorId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postRepository.GetByAuthorIdAsync(
            authorId,
            pagination.Limit,
            cursor,
            currentUserId,
            cancellationToken);
        var hasMore = posts.Count > pagination.Limit;
        var pagePosts = posts.Take(pagination.Limit).ToList();
        var items = pagePosts
            .Select(_responseFactory.Create)
            .ToList();

        return new CursorPageResponse<PostResponse>
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.Encode(pagePosts[^1].CreatedAt, pagePosts[^1].Id)
                : null
        };
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetLikedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetLikedByUserIdAsync(
            userId,
            limit,
            cursor,
            currentUserId,
            cancellationToken);

        return posts
            .Select(_responseFactory.Create)
            .ToList();
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetBookmarkedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetBookmarkedByUserIdAsync(
            userId,
            limit,
            cursor,
            currentUserId,
            cancellationToken);

        return posts
            .Select(_responseFactory.Create)
            .ToList();
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        int limit,
        CursorPosition? cursor = null,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetRepostedByUserIdAsync(
            userId,
            limit,
            cursor,
            currentUserId,
            cancellationToken);

        return posts
            .Select(_responseFactory.Create)
            .ToList();
    }

    public async Task<CursorPageResponse<PostResponse>> SearchAsync(
        string query,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var normalizedQuery = SearchQueryNormalizer.Normalize(
            query,
            MaximumSearchQueryLength,
            "Post search query");

        if (normalizedQuery is null)
        {
            return new CursorPageResponse<PostResponse>
            {
                Items = [],
                HasMore = false,
                NextCursor = null
            };
        }

        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postRepository.SearchAsync(
            normalizedQuery,
            pagination.Limit,
            cursor,
            currentUserId,
            cancellationToken);
        var hasMore = posts.Count > pagination.Limit;
        var pagePosts = posts.Take(pagination.Limit).ToList();

        return new CursorPageResponse<PostResponse>
        {
            Items = pagePosts
                .Select(_responseFactory.Create)
                .ToList(),
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.Encode(pagePosts[^1].CreatedAt, pagePosts[^1].Id)
                : null
        };
    }

    public async Task<PostResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cacheKey = PostCache.GetKey(id);
        var post = await _cache.GetOrCreateAsync<PostContentReadModel?>(
            cacheKey,
            async token => await _postRepository.GetContentByIdAsync(id, token),
            PostCache.EntryOptions,
            cancellationToken: cancellationToken);

        if (post is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, _logger, cacheKey);
            return null;
        }

        var engagement = await _postRepository.GetEngagementByIdAsync(
            id,
            currentUserId,
            cancellationToken);

        if (engagement is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, _logger, cacheKey);
            return null;
        }

        if (post.UpdatedAt != engagement.UpdatedAt)
        {
            var refreshedPost = await _postRepository.GetContentByIdAsync(id, cancellationToken);

            if (refreshedPost is null)
            {
                await CacheInvalidation.TryRemoveAsync(_cache, _logger, cacheKey);
                return null;
            }

            post = refreshedPost;
            await PostCache.TrySetAsync(_cache, cacheKey, post, _logger);
        }

        var author = await _userService.GetByIdAsync(post.AuthorId, cancellationToken);

        if (author is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, _logger, cacheKey);
            return null;
        }

        return _responseFactory.Create(post, engagement, author);
    }

    public async Task<int> GetViewCountAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var viewCounts = await _postRepository.GetViewCountsAsync([postId], cancellationToken);

        return viewCounts.GetValueOrDefault(postId);
    }

}
