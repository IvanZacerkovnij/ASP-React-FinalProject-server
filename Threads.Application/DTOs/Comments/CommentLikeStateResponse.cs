namespace Threads.Application.DTOs.Comments;

public sealed class CommentLikeStateResponse
{
    public bool LikedByMe { get; init; }

    public int LikesCount { get; init; }
}
