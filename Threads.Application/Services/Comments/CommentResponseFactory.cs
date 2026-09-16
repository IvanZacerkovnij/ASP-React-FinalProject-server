using AutoMapper;
using Threads.Application.DTOs.Comments;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Comments;

public sealed class CommentResponseFactory
{
    private readonly UserResponseFactory _userResponseFactory;
    private readonly IMapper _mapper;

    public CommentResponseFactory(
        UserResponseFactory userResponseFactory,
        IMapper mapper)
    {
        _userResponseFactory = userResponseFactory;
        _mapper = mapper;
    }

    public CommentResponse Create(
        Comment comment,
        Guid? currentUserId,
        int viewsCount,
        DateTimeOffset? actionAt = null)
    {
        var response = _mapper.Map<CommentResponse>(comment);

        return new CommentResponse
        {
            Id = response.Id,
            PostId = response.PostId,
            ParentCommentId = response.ParentCommentId,
            Content = response.Content,
            Author = _userResponseFactory.CreateShort(comment.Author),
            LikesCount = response.LikesCount,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                comment.CommentLikes.Any(like => like.UserId == currentUserId.Value),
            RepliesCount = response.RepliesCount,
            IsBookmarkedByCurrentUser = currentUserId.HasValue &&
                comment.CommentBookmarks.Any(bookmark => bookmark.UserId == currentUserId.Value),
            RepostsCount = response.RepostsCount,
            IsRepostedByCurrentUser = currentUserId.HasValue &&
                comment.CommentReposts.Any(repost => repost.UserId == currentUserId.Value),
            ViewsCount = viewsCount,
            ActionAt = actionAt,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt
        };
    }
}
