using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.ScheduledPosts;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.ScheduledPosts;
using Threads.Application.Services.Common;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.ScheduledPosts;

public sealed class ScheduledPostService : IScheduledPostService
{
    private const int PublishBatchSize = 20;

    private readonly IScheduledPostRepository _scheduledPostRepository;
    private readonly ScheduledPostMediaManager _mediaManager;
    private readonly ScheduledPostResponseFactory _responseFactory;
    private readonly PostVersionFactory _postVersionFactory;
    private readonly HybridCache _cache;
    private readonly ILogger<ScheduledPostService> _logger;

    public ScheduledPostService(
        IScheduledPostRepository scheduledPostRepository,
        ScheduledPostMediaManager mediaManager,
        ScheduledPostResponseFactory responseFactory,
        PostVersionFactory postVersionFactory,
        HybridCache cache,
        ILogger<ScheduledPostService> logger)
    {
        _scheduledPostRepository = scheduledPostRepository;
        _mediaManager = mediaManager;
        _responseFactory = responseFactory;
        _postVersionFactory = postVersionFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ScheduledPostResponse>> GetAsync(
        Guid authorId,
        CancellationToken cancellationToken = default)
    {
        var scheduledPosts = await _scheduledPostRepository.GetByAuthorIdAsync(
            authorId,
            cancellationToken);

        return scheduledPosts.Select(_responseFactory.Create).ToList();
    }

    public async Task<ScheduledPostResponse> CreateAsync(
        Guid authorId,
        CreateScheduledPostRequest request,
        CancellationToken cancellationToken = default)
    {
        RequestValidator.Validate(request);
        var normalizedPost = CreateNormalizedPost(
            authorId,
            request.Content,
            request.MediaIds,
            request.LinkPreview);
        var scheduledPost = new ScheduledPost
        {
            AuthorId = authorId,
            ScheduledAt = request.ScheduledAt
        };
        ApplyPostFields(scheduledPost, normalizedPost);
        await _mediaManager.ApplyAsync(
            scheduledPost,
            authorId,
            request.MediaIds,
            cancellationToken);
        await _scheduledPostRepository.AddAsync(scheduledPost, cancellationToken);

        return _responseFactory.Create(scheduledPost);
    }

    public async Task<ScheduledPostResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdateScheduledPostRequest request,
        CancellationToken cancellationToken = default)
    {
        RequestValidator.Validate(request);
        var scheduledPost = await GetOwnedAsync(id, currentUserId, cancellationToken);
        var content = request.HasContentValue ? request.Content : scheduledPost.Content;
        var mediaIds = request.MediaIds ?? scheduledPost.Media
            .OrderBy(media => media.SortOrder)
            .Select(media => media.Id)
            .ToArray();
        var linkPreview = request.HasLinkPreviewValue
            ? request.LinkPreview
            : CreateLinkPreviewRequest(scheduledPost);
        var scheduledAt = request.HasScheduledAtValue
            ? request.ScheduledAt ?? throw new RequestValidationException("Scheduled time is required.")
            : scheduledPost.ScheduledAt;
        var normalizedPost = CreateNormalizedPost(
            currentUserId,
            content,
            mediaIds,
            linkPreview);

        if (request.MediaIds is not null)
        {
            await _mediaManager.ApplyAsync(
                scheduledPost,
                currentUserId,
                request.MediaIds,
                cancellationToken);
        }

        ApplyPostFields(scheduledPost, normalizedPost);
        scheduledPost.ScheduledAt = scheduledAt;
        scheduledPost.UpdatedAt = DateTimeOffset.UtcNow;
        await _scheduledPostRepository.UpdateAsync(scheduledPost, cancellationToken);

        return _responseFactory.Create(scheduledPost);
    }

    public async Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scheduledPost = await GetOwnedAsync(id, currentUserId, cancellationToken);

        foreach (var media in scheduledPost.Media.ToList())
        {
            media.ScheduledPostId = null;
            media.SortOrder = 0;
        }

        scheduledPost.Media.Clear();
        await _scheduledPostRepository.DeleteAsync(scheduledPost, cancellationToken);
    }

    public async Task<int> PublishDueAsync(CancellationToken cancellationToken = default)
    {
        var duePosts = await _scheduledPostRepository.GetDueAsync(
            DateTimeOffset.UtcNow,
            PublishBatchSize,
            cancellationToken);
        var publishedCount = 0;

        foreach (var scheduledPost in duePosts)
        {
            var post = CreateNormalizedPost(
                scheduledPost.AuthorId,
                scheduledPost.Content,
                scheduledPost.Media.Select(media => media.Id).ToArray(),
                CreateLinkPreviewRequest(scheduledPost));

            foreach (var media in scheduledPost.Media.ToList())
            {
                media.ScheduledPostId = null;
                media.PostId = post.Id;
                scheduledPost.Media.Remove(media);
                post.Media.Add(media);
            }

            post.Versions.Add(_postVersionFactory.Create(post));
            await _scheduledPostRepository.PublishAsync(scheduledPost, post, cancellationToken);
            await CacheInvalidation.TryRemoveAsync(
                _cache,
                _logger,
                UserProfileCache.GetProfileKey(scheduledPost.AuthorId));
            publishedCount++;
        }

        return publishedCount;
    }

    private async Task<ScheduledPost> GetOwnedAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var scheduledPost = await _scheduledPostRepository.GetByIdAsync(id, cancellationToken);

        if (scheduledPost is null)
        {
            throw new NotFoundException("Scheduled post was not found.");
        }

        if (scheduledPost.AuthorId != currentUserId)
        {
            throw new ForbiddenException("You cannot modify this scheduled post.");
        }

        return scheduledPost;
    }

    private static Post CreateNormalizedPost(
        Guid authorId,
        string? content,
        IReadOnlyCollection<Guid> mediaIds,
        LinkPreviewRequest? linkPreview)
    {
        return PostInputMapper.Create(
            authorId,
            new CreatePostRequest
            {
                Content = content,
                MediaIds = mediaIds,
                LinkPreview = linkPreview
            });
    }

    private static void ApplyPostFields(ScheduledPost scheduledPost, Post normalizedPost)
    {
        scheduledPost.Content = normalizedPost.Content;
        scheduledPost.LinkPreviewUrl = normalizedPost.EmbedUrl;
        scheduledPost.LinkPreviewTitle = normalizedPost.EmbedTitle;
        scheduledPost.LinkPreviewImageUrl = normalizedPost.EmbedThumbnailUrl;
    }

    private static LinkPreviewRequest? CreateLinkPreviewRequest(ScheduledPost scheduledPost)
    {
        return string.IsNullOrWhiteSpace(scheduledPost.LinkPreviewUrl)
            ? null
            : new LinkPreviewRequest
            {
                Url = scheduledPost.LinkPreviewUrl,
                Title = scheduledPost.LinkPreviewTitle,
                ImageUrl = scheduledPost.LinkPreviewImageUrl
            };
    }

}
