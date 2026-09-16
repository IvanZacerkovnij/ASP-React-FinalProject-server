using Microsoft.Extensions.Caching.Hybrid;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Posts;

public sealed class PostQueryService
{
    private const int FeedSize = 10;

    private readonly IPostRepository _postRepository;
    private readonly IUserService _userService;
    private readonly PostResponseFactory _responseFactory;
    private readonly HybridCache _cache;

    public PostQueryService(
        IPostRepository postRepository,
        IUserService userService,
        PostResponseFactory responseFactory,
        HybridCache cache)
    {
        _postRepository = postRepository;
        _userService = userService;
        _responseFactory = responseFactory;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetRandomAsync(FeedSize, cancellationToken);
        var viewCounts = await GetViewCountsAsync(posts, cancellationToken);

        return posts
            .Select(post => _responseFactory.Create(
                post,
                currentUserId,
                viewCounts.GetValueOrDefault(post.Id)))
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
            cancellationToken);
        var hasMore = posts.Count > pagination.Limit;
        var pagePosts = posts.Take(pagination.Limit).ToList();
        var viewCounts = await GetViewCountsAsync(pagePosts, cancellationToken);
        var items = pagePosts
            .Select(post => _responseFactory.Create(
                post,
                currentUserId,
                viewCounts.GetValueOrDefault(post.Id)))
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
            cancellationToken);
        var viewCounts = await GetViewCountsAsync(posts, cancellationToken);

        return posts
            .Select(post => _responseFactory.Create(
                post,
                currentUserId,
                viewCounts.GetValueOrDefault(post.Id),
                post.PostLikes.FirstOrDefault(like => like.UserId == userId)?.CreatedAt))
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
            cancellationToken);
        var viewCounts = await GetViewCountsAsync(posts, cancellationToken);

        return posts
            .Select(post => _responseFactory.Create(
                post,
                currentUserId,
                viewCounts.GetValueOrDefault(post.Id),
                post.PostBookmarks.FirstOrDefault(bookmark => bookmark.UserId == userId)?.CreatedAt))
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
            cancellationToken);
        var viewCounts = await GetViewCountsAsync(posts, cancellationToken);

        return posts
            .Select(post => _responseFactory.Create(
                post,
                currentUserId,
                viewCounts.GetValueOrDefault(post.Id),
                post.PostReposts.FirstOrDefault(repost => repost.UserId == userId)?.CreatedAt))
            .ToList();
    }

    public async Task<CursorPageResponse<PostResponse>> SearchAsync(
        string query,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
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
            query.Trim(),
            pagination.Limit,
            cursor,
            cancellationToken);
        var hasMore = posts.Count > pagination.Limit;
        var pagePosts = posts.Take(pagination.Limit).ToList();
        var viewCounts = await GetViewCountsAsync(pagePosts, cancellationToken);

        return new CursorPageResponse<PostResponse>
        {
            Items = pagePosts
                .Select(post => _responseFactory.Create(
                    post,
                    currentUserId,
                    viewCounts.GetValueOrDefault(post.Id)))
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
        var post = await _cache.GetOrCreateAsync<PostReadModel?>(
            cacheKey,
            async token => await _postRepository.GetReadModelByIdAsync(id, token),
            PostCache.EntryOptions,
            cancellationToken: cancellationToken);

        if (post is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, cacheKey);
            return null;
        }

        var state = await _postRepository.GetStateByIdAsync(
            id,
            currentUserId,
            cancellationToken);

        if (state is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, cacheKey);
            return null;
        }

        if (post.UpdatedAt != state.UpdatedAt)
        {
            var refreshedPost = await _postRepository.GetReadModelByIdAsync(id, cancellationToken);

            if (refreshedPost is null)
            {
                await CacheInvalidation.TryRemoveAsync(_cache, cacheKey);
                return null;
            }

            post = refreshedPost;
            await PostCache.TrySetAsync(_cache, cacheKey, post);
        }

        var author = await _userService.GetByIdAsync(post.AuthorId, cancellationToken);

        if (author is null)
        {
            await CacheInvalidation.TryRemoveAsync(_cache, cacheKey);
            return null;
        }

        return _responseFactory.Create(post, state, author);
    }

    public async Task<int> GetViewCountAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var viewCounts = await _postRepository.GetViewCountsAsync([postId], cancellationToken);

        return viewCounts.GetValueOrDefault(postId);
    }

    private async Task<IReadOnlyDictionary<Guid, int>> GetViewCountsAsync(
        IReadOnlyCollection<Post> posts,
        CancellationToken cancellationToken)
    {
        return await _postRepository.GetViewCountsAsync(
            posts.Select(post => post.Id).ToArray(),
            cancellationToken);
    }
}
