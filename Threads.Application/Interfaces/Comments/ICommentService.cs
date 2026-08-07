using Threads.Application.DTOs.Comments;

namespace Threads.Application.Interfaces.Comments;

public interface ICommentService
{
    Task<IReadOnlyCollection<CommentResponse>> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task<CommentResponse> CreateAsync(Guid authorId, CreateCommentRequest request, CancellationToken cancellationToken = default);
    Task<CommentResponse?> UpdateAsync(Guid id, UpdateCommentRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
