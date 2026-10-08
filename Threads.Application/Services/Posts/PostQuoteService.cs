using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Application.Services.Posts;

public sealed class PostQuoteService
{
    private readonly IPostRepository _postRepository;
    private readonly ICommentRepository _commentRepository;

    public PostQuoteService(
        IPostRepository postRepository,
        ICommentRepository commentRepository)
    {
        _postRepository = postRepository;
        _commentRepository = commentRepository;
    }

    public async Task<PostQuote?> CreateAsync(
        Post sourcePost,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePost);
        ArgumentNullException.ThrowIfNull(request);

        if (request.QuotedPostId.HasValue &&
            request.QuotedCommentId.HasValue)
        {
            throw new RequestValidationException(
                "Only one quoted target may be specified.");
        }

        var hasQuotedTarget =
            request.QuotedPostId.HasValue ||
            request.QuotedCommentId.HasValue;

        if (!hasQuotedTarget)
        {
            if (request.QuotedTargetVersionId.HasValue)
            {
                throw new RequestValidationException(
                    "Quoted target version requires a quoted target.");
            }

            return null;
        }

        if (!request.QuotedTargetVersionId.HasValue)
        {
            throw new RequestValidationException(
                "Quoted target version is required.");
        }

        var targetType = request.QuotedPostId.HasValue
            ? ContentTargetType.Post
            : ContentTargetType.Comment;

        var targetId =
            request.QuotedPostId ??
            request.QuotedCommentId!.Value;

        var targetVersionId = request.QuotedTargetVersionId.Value;

        var versionExists = targetType == ContentTargetType.Post
            ? await _postRepository.VersionExistsAsync(
                targetId,
                targetVersionId,
                cancellationToken)
            : await _commentRepository.VersionExistsAsync(
                targetId,
                targetVersionId,
                cancellationToken);
        if (!versionExists)
        {
            throw new NotFoundException(
                "Quoted target version was not found.");
        }

        return new PostQuote
        {
            PostId = sourcePost.Id,
            Post = sourcePost,
            TargetType = targetType,
            TargetId = targetId,
            TargetVersionId = targetVersionId
        };
    }
}