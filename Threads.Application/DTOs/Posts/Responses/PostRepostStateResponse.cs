namespace Threads.Application.DTOs.Posts.Responses;

public sealed class PostRepostStateResponse
{
    public bool RepostedByMe { get; init; }

    public int RepostsCount { get; init; }
}
