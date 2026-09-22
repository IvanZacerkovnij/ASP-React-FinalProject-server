namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostEngagementReadModel
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

    public PostPollEngagementReadModel? Poll { get; init; }
}
