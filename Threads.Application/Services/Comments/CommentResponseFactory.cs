using Threads.Application.DTOs.Comments;
using Threads.Application.Services.Users;

namespace Threads.Application.Services.Comments;

public sealed class CommentResponseFactory
{
    private readonly UserResponseFactory _userResponseFactory;

    public CommentResponseFactory(UserResponseFactory userResponseFactory)
    {
        _userResponseFactory = userResponseFactory;
    }

    public CommentResponse Create(CommentSummaryReadModel comment)
    {
        return new CommentResponse
        {
            Id = comment.Id,
            PostId = comment.PostId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            Author = _userResponseFactory.CreateShort(comment.Author),
            LikesCount = comment.LikesCount,
            IsLikedByCurrentUser = comment.IsLikedByCurrentUser,
            RepliesCount = comment.RepliesCount,
            IsBookmarkedByCurrentUser = comment.IsBookmarkedByCurrentUser,
            RepostsCount = comment.RepostsCount,
            IsRepostedByCurrentUser = comment.IsRepostedByCurrentUser,
            ViewsCount = comment.ViewsCount,
            ActionAt = comment.ActionAt,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };
    }
}
