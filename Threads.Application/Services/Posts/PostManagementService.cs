using Microsoft.Extensions.Caching.Hybrid;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Common;
using Threads.Application.Services.Users;

namespace Threads.Application.Services.Posts;

public sealed class PostManagementService
{
    private readonly IPostRepository _postRepository;
    private readonly PostMediaManager _postMediaManager;
    private readonly PostQueryService _postQueryService;
    private readonly PostResponseFactory _responseFactory;
    private readonly HybridCache _cache;

    public PostManagementService(
        IPostRepository postRepository,
        PostMediaManager postMediaManager,
        PostQueryService postQueryService,
        PostResponseFactory responseFactory,
        HybridCache cache)
    {
        _postRepository = postRepository;
        _postMediaManager = postMediaManager;
        _postQueryService = postQueryService;
        _responseFactory = responseFactory;
        _cache = cache;
    }

    public async Task<PostResponse> CreateAsync(
        Guid authorId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = PostInputMapper.Create(authorId, request);
        var mediaIds = request.MediaIds ?? [];

        await _postMediaManager.ApplyAsync(post, authorId, mediaIds, cancellationToken);
        await _postRepository.AddAsync(post, cancellationToken);
        await CacheInvalidation.TryRemoveAsync(_cache, UserProfileCache.GetProfileKey(authorId));

        var createdPost = await _postRepository.GetByIdAsync(post.Id, cancellationToken);

        return _responseFactory.Create(createdPost ?? post, authorId, viewsCount: 0);
    }

    public async Task<PostResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        if (post is null)
        {
            throw new NotFoundException("Post was not found.");
        }

        if (post.AuthorId != currentUserId)
        {
            throw new ForbiddenException("You cannot update this post.");
        }

        PostInputMapper.ApplyContent(post, request.Content);

        if (request.MediaIds is not null)
        {
            await _postMediaManager.ApplyAsync(
                post,
                post.AuthorId,
                request.MediaIds,
                cancellationToken);
        }

        PostInputMapper.ApplyMetadataChanges(post, request);
        PostInputMapper.ValidateState(post);
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await _postRepository.UpdateAsync(post, cancellationToken);
        await CacheInvalidation.TryRemoveAsync(_cache, PostCache.GetKey(post.Id));

        var updatedPost = await _postRepository.GetByIdAsync(post.Id, cancellationToken);
        var viewsCount = await _postQueryService.GetViewCountAsync(post.Id, cancellationToken);

        return _responseFactory.Create(updatedPost ?? post, post.AuthorId, viewsCount);
    }

    public async Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        if (post is null)
        {
            throw new NotFoundException("Post was not found.");
        }

        if (post.AuthorId != currentUserId)
        {
            throw new ForbiddenException("You cannot delete this post.");
        }

        var mediaStorageKeys = post.Media
            .SelectMany(media => new[]
            {
                media.StorageKey,
                media.ThumbnailStorageKey
            })
            .Where(storageKey => !string.IsNullOrWhiteSpace(storageKey))
            .Cast<string>()
            .Distinct()
            .ToArray();

        await _postRepository.DeleteAsync(post, cancellationToken);
        await CacheInvalidation.TryRemoveAsync(
            _cache,
            PostCache.GetKey(post.Id),
            UserProfileCache.GetProfileKey(post.AuthorId));
        await _postMediaManager.TryDeleteAsync(mediaStorageKeys, cancellationToken);
    }
}
