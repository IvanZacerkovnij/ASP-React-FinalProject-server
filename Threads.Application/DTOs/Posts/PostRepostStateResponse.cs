namespace Threads.Application.DTOs.Posts;

public sealed class PostRepostStateResponse
{
    public bool RepostedByMe { get; init; }

    public int RepostsCount { get; init; }
}
