using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Search;
using Threads.Application.DTOs.Quotes;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Application.Services.Posts;

public sealed class PostQueryService
{
    private const int FeedSize = 10;
    private const int MaximumSearchQueryLength = 100;

    private readonly IPostRepository _postRepository;
    private readonly IUserService _userService;
    private readonly PostResponseFactory _responseFactory;
    private readonly CommentQueryService _commentQueryService;
    private readonly HybridCache _cache;
    private readonly ILogger<PostQueryService> _logger;

    public PostQueryService(
        IPostRepository postRepository,
        IUserService userService,
        PostResponseFactory responseFactory,
        CommentQueryService commentQueryService,
        HybridCache cache,
        ILogger<PostQueryService> logger)
    {
        _postRepository = postRepository;
        _userService = userService;
        _responseFactory = responseFactory;
        _commentQueryService = commentQueryService;
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

        return await CreateResponsesAsync(posts, currentUserId, cancellationToken);
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
        var items = await CreateResponsesAsync(pagePosts, currentUserId, cancellationToken);

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

        return await CreateResponsesAsync(posts, currentUserId, cancellationToken);
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

        return await CreateResponsesAsync(posts, currentUserId, cancellationToken);
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

        return await CreateResponsesAsync(posts, currentUserId, cancellationToken);
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
        return await CreateSearchPageAsync(posts, pagination.Limit, currentUserId, cancellationToken);
    }

    public async Task<CursorPageResponse<PostResponse>> SearchAsync(
        SearchPostsRequest request,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        RequestValidator.Validate(request);
        var normalizedQuery = SearchQueryNormalizer.Normalize(
            request.Q,
            MaximumSearchQueryLength,
            "Post search query");
        var people = SearchFilterNormalizer.NormalizeOption(
            request.People,
            "following",
            "People");
        var location = SearchFilterNormalizer.NormalizeOption(
            request.Location,
            "near",
            "Location");
        var exactPhrase = SearchFilterNormalizer.NormalizeText(request.ExactPhrase);
        var anyWords = SearchFilterNormalizer.SplitWords(request.AnyWords);
        var excludeWords = SearchFilterNormalizer.SplitWords(request.ExcludeWords);
        var from = SearchFilterNormalizer.NormalizeText(request.From)?.ToLowerInvariant();

        if (normalizedQuery is null && people is null && location is null &&
            exactPhrase is null && anyWords.Count == 0 && excludeWords.Count == 0 &&
            from is null && !request.MinReplies.HasValue && !request.MinLikes.HasValue &&
            !request.MinReposts.HasValue && !request.FromDate.HasValue &&
            !request.ToDate.HasValue && !request.HasMedia.HasValue)
        {
            return new CursorPageResponse<PostResponse>
            {
                Items = [],
                HasMore = false,
                NextCursor = null
            };
        }

        var cursor = CursorCodec.Decode(request.Cursor);
        var posts = await _postRepository.SearchAsync(
            normalizedQuery,
            people,
            location,
            exactPhrase,
            anyWords,
            excludeWords,
            from,
            request.MinReplies,
            request.MinLikes,
            request.MinReposts,
            request.FromDate,
            request.ToDate,
            request.HasMedia,
            request.Limit,
            cursor,
            currentUserId,
            cancellationToken);
        return await CreateSearchPageAsync(
            posts,
            request.Limit,
            currentUserId,
            cancellationToken);
    }

    private async Task<CursorPageResponse<PostResponse>> CreateSearchPageAsync(
        IReadOnlyCollection<PostSummaryReadModel> posts,
        int limit,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var hasMore = posts.Count > limit;
        var pagePosts = posts.Take(limit).ToList();

        var items = await CreateResponsesAsync(pagePosts, currentUserId, cancellationToken);

        return new CursorPageResponse<PostResponse>
        {
            Items = items,
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
        return await GetByIdAsync(id, includeQuote: true, cancellationToken, currentUserId);
    }

    private async Task<PostResponse?> GetByIdAsync(
        Guid id,
        bool includeQuote,
        CancellationToken cancellationToken,
        Guid? currentUserId)
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

        var response = _responseFactory.Create(post, engagement, author);

        if (includeQuote)
        {
            response.Quote = await CreateQuoteAsync(post.Quote, currentUserId, cancellationToken);
        }

        return response;
    }

    public async Task<int> GetViewCountAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var viewCounts = await _postRepository.GetViewCountsAsync([postId], cancellationToken);

        return viewCounts.GetValueOrDefault(postId);
    }

    public Task<QuoteResponse?> CreateQuoteAsync(
        PostQuote? quote,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (quote is null)
        {
            return Task.FromResult<QuoteResponse?>(null);
        }

        return CreateQuoteAsync(
            new QuoteReadModel
            {
                TargetType = quote.TargetType,
                TargetId = quote.TargetId,
                TargetVersionId = quote.TargetVersionId
            },
            currentUserId,
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<PostResponse>> CreateResponsesAsync(
        IEnumerable<PostSummaryReadModel> posts,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var responses = new List<PostResponse>();

        foreach (var post in posts)
        {
            var response = _responseFactory.Create(post);
            response.Quote = await CreateQuoteAsync(post.Quote, currentUserId, cancellationToken);
            responses.Add(response);
        }

        return responses;
    }

    private async Task<QuoteResponse?> CreateQuoteAsync(
        QuoteReadModel? quote,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        if (quote is null)
        {
            return null;
        }

        object? target = quote.TargetType switch
        {
            ContentTargetType.Post => await GetByIdAsync(
                quote.TargetId,
                includeQuote: false,
                cancellationToken,
                currentUserId),
            ContentTargetType.Comment => await _commentQueryService.GetByIdAsync(
                quote.TargetId,
                cancellationToken,
                currentUserId),
            _ => null
        };

        var currentVersionId = target switch
        {
            PostResponse post => post.VersionId,
            CommentResponse comment => comment.VersionId,
            _ => (Guid?)null
        };

        return new QuoteResponse
        {
            TargetType = quote.TargetType,
            TargetId = quote.TargetId,
            TargetVersionId = quote.TargetVersionId,
            HasNewVersion = currentVersionId.HasValue &&
                currentVersionId.Value != quote.TargetVersionId,
            Target = target
        };
    }

}
