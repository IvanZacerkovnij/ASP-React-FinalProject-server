namespace Threads.Application.DTOs.Posts.Responses;

public class PostBookmarkStateResponse
{
    public bool BookmarkedByMe { get; init; }
    public int BookmarksCount { get; init; }
}