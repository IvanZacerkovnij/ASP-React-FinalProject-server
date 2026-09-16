namespace Threads.Application.DTOs.Comments;

public sealed class CommentRepostStateResponse
{
    public bool RepostedByMe { get; init; }

    public int RepostsCount { get; init; }
}
