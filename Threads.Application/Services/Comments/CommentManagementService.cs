using AutoMapper;
using Threads.Application.DTOs.Comments;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Comments;

public sealed class CommentManagementService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly CommentMediaManager _commentMediaManager;
    private readonly CommentQueryService _commentQueryService;
    private readonly CommentVersionFactory _commentVersionFactory;
    private readonly IMapper _mapper;

    public CommentManagementService(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        CommentMediaManager commentMediaManager,
        CommentQueryService commentQueryService,
        CommentVersionFactory commentVersionFactory,
        IMapper mapper)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _commentMediaManager = commentMediaManager;
        _commentQueryService = commentQueryService;
        _commentVersionFactory = commentVersionFactory;
        _mapper = mapper;
    }

    public async Task<CommentResponse> CreateAsync(
        Guid authorId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateContent(request.Content);

        if (!await _postRepository.ExistsAsync(request.PostId, cancellationToken))
        {
            throw new NotFoundException("Post was not found.");
        }

        if (request.ParentCommentId.HasValue)
        {
            var parentPostId = await _commentRepository.GetPostIdByIdAsync(
                request.ParentCommentId.Value,
                cancellationToken);

            if (!parentPostId.HasValue)
            {
                throw new NotFoundException("Parent comment was not found.");
            }

            if (parentPostId.Value != request.PostId)
            {
                throw new RequestValidationException(
                    "Parent comment does not belong to the specified post.");
            }
        }

        var comment = _mapper.Map<Comment>(request);
        comment.AuthorId = authorId;
        comment.Content = request.Content.Trim();
        comment.ParentCommentId = request.ParentCommentId;
        CommentInputMapper.ApplyCreateMetadata(comment, request);
        await _commentMediaManager.ApplyAsync(
            comment,
            authorId,
            request.MediaIds,
            cancellationToken);
        comment.Versions.Add(_commentVersionFactory.Create(comment));

        await _commentRepository.AddAsync(comment, cancellationToken);

        var createdComment = await _commentQueryService.GetByIdAsync(
            comment.Id,
            cancellationToken,
            authorId);

        return createdComment ?? throw new InvalidOperationException("Created comment was not found.");
    }

    public async Task<CommentResponse> UpdateAsync(
        Guid id,
        Guid currentUserId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException("Comment was not found.");
        }

        if (comment.AuthorId != currentUserId)
        {
            throw new ForbiddenException("You cannot update this comment.");
        }

        ValidateContent(request.Content);

        comment.Content = request.Content.Trim();

        if (request.MediaIds is not null)
        {
            await _commentMediaManager.ApplyAsync(
                comment,
                comment.AuthorId,
                request.MediaIds,
                cancellationToken);
        }

        var removedPoll = CommentInputMapper.ApplyMetadataChanges(comment, request);

        if (removedPoll is not null)
        {
            _commentRepository.RemovePoll(removedPoll);
        }

        comment.CurrentVersionId = Guid.NewGuid();
        comment.Versions.Add(_commentVersionFactory.Create(comment));
        comment.UpdatedAt = DateTimeOffset.UtcNow;

        await _commentRepository.UpdateAsync(comment, cancellationToken);

        var updatedComment = await _commentQueryService.GetByIdAsync(
            comment.Id,
            cancellationToken,
            currentUserId);

        return updatedComment ?? throw new InvalidOperationException("Updated comment was not found.");
    }

    public async Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException("Comment was not found.");
        }

        if (comment.AuthorId != currentUserId)
        {
            throw new ForbiddenException("You cannot delete this comment.");
        }

        var mediaStorageKeys = comment.Media
            .SelectMany(media => new[]
            {
                media.StorageKey,
                media.ThumbnailStorageKey
            })
            .Where(storageKey => !string.IsNullOrWhiteSpace(storageKey))
            .Cast<string>()
            .Distinct()
            .ToArray();

        await _commentRepository.DeleteAsync(comment, cancellationToken);
        await _commentMediaManager.TryDeleteAsync(mediaStorageKeys, cancellationToken);
    }

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new RequestValidationException("Comment content is required.");
        }
    }

}
