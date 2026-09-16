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
    private readonly CommentQueryService _commentQueryService;
    private readonly CommentResponseFactory _responseFactory;
    private readonly IMapper _mapper;

    public CommentManagementService(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        CommentQueryService commentQueryService,
        CommentResponseFactory responseFactory,
        IMapper mapper)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _commentQueryService = commentQueryService;
        _responseFactory = responseFactory;
        _mapper = mapper;
    }

    public async Task<CommentResponse> CreateAsync(
        Guid authorId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateContent(request.Content);

        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            throw new NotFoundException("Post was not found.");
        }

        Comment? parentComment = null;

        if (request.ParentCommentId.HasValue)
        {
            parentComment = await _commentRepository.GetByIdAsync(
                request.ParentCommentId.Value,
                cancellationToken);

            if (parentComment is null)
            {
                throw new NotFoundException("Parent comment was not found.");
            }

            if (parentComment.PostId != request.PostId)
            {
                throw new RequestValidationException(
                    "Parent comment does not belong to the specified post.");
            }
        }

        var comment = _mapper.Map<Comment>(request);
        comment.AuthorId = authorId;
        comment.Content = request.Content.Trim();
        comment.ParentCommentId = parentComment?.Id;

        await _commentRepository.AddAsync(comment, cancellationToken);

        var createdComment = await _commentRepository.GetByIdAsync(comment.Id, cancellationToken);

        return _responseFactory.Create(createdComment ?? comment, authorId, viewsCount: 0);
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
        comment.UpdatedAt = DateTimeOffset.UtcNow;

        await _commentRepository.UpdateAsync(comment, cancellationToken);

        var updatedComment = await _commentRepository.GetByIdAsync(comment.Id, cancellationToken);
        var viewsCount = await _commentQueryService.GetViewCountAsync(comment.Id, cancellationToken);

        return _responseFactory.Create(updatedComment ?? comment, currentUserId, viewsCount);
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

        await _commentRepository.DeleteAsync(comment, cancellationToken);
    }

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new RequestValidationException("Comment content is required.");
        }
    }
}
