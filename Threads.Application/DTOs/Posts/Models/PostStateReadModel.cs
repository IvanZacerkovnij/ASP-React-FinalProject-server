namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostStateReadModel
{
    public DateTimeOffset? UpdatedAt { get; init; }

    public int LikesCount { get; init; }

    public int CommentsCount { get; init; }

    public int RepostsCount { get; init; }

    public int BookmarksCount { get; init; }

    public int ViewsCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }

    public bool IsBookmarkedByCurrentUser { get; init; }

    public PostPollStateReadModel? Poll { get; init; }
}
