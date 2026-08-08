using AutoMapper;
using Threads.Application.DTOs.Comments;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class CommentService : ICommentService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly IMapper _mapper;

    public CommentService(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IMapper mapper)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<CommentResponse>> GetByPostIdAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        var comments = await _commentRepository.GetByPostIdAsync(postId, cancellationToken);

        return comments
            .Select(_mapper.Map<CommentResponse>)
            .ToList();
    }

    public async Task<CommentResponse> CreateAsync(
        Guid authorId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            throw new InvalidOperationException("Post was not found.");
        }

        var comment = _mapper.Map<Comment>(request);
        comment.AuthorId = authorId;

        await _commentRepository.AddAsync(comment, cancellationToken);

        var createdComment = await _commentRepository.GetByIdAsync(comment.Id, cancellationToken);

        return _mapper.Map<CommentResponse>(createdComment ?? comment);
    }

    public async Task<CommentResponse?> UpdateAsync(
        Guid id,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return null;
        }

        comment.Content = request.Content;
        comment.UpdatedAt = DateTimeOffset.UtcNow;

        await _commentRepository.UpdateAsync(comment, cancellationToken);

        var updatedComment = await _commentRepository.GetByIdAsync(comment.Id, cancellationToken);

        return _mapper.Map<CommentResponse>(updatedComment ?? comment);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var comment = await _commentRepository.GetByIdAsync(id, cancellationToken);

        if (comment is null)
        {
            return false;
        }

        await _commentRepository.DeleteAsync(comment, cancellationToken);

        return true;
    }
}
