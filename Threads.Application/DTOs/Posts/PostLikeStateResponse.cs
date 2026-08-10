namespace Threads.Application.DTOs.Posts;

public sealed class PostLikeStateResponse
{
    public bool LikedByMe { get; init; }

    public int LikesCount { get; init; }
}
