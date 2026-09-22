using Threads.Application.DTOs.Users;

namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostSummaryReadModel
{
    public Guid Id { get; init; }

    public string? Content { get; init; }

    public required UserSummaryReadModel Author { get; init; }

    public IReadOnlyCollection<PostMediaReadModel> Media { get; init; } = [];

    public PostPollSummaryReadModel? Poll { get; init; }

    public string? LocationPlaceId { get; init; }

    public string? LocationName { get; init; }

    public string? LocationCountry { get; init; }

    public double? LocationLatitude { get; init; }

    public double? LocationLongitude { get; init; }

    public string? EmbedUrl { get; init; }

    public string? EmbedTitle { get; init; }

    public string? EmbedDescription { get; init; }

    public string? EmbedThumbnailUrl { get; init; }

    public int LikesCount { get; init; }

    public int CommentsCount { get; init; }

    public int RepostsCount { get; init; }

    public int BookmarksCount { get; init; }

    public int ViewsCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }

    public bool IsBookmarkedByCurrentUser { get; init; }

    public DateTimeOffset? ActionAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
