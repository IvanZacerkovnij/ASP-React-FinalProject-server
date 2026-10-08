using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Posts.Models;

namespace Threads.Application.DTOs.Comments;

public sealed class CommentSummaryReadModel
{
    public Guid Id { get; init; }

    public Guid VersionId { get; init; }

    public Guid PostId { get; init; }

    public Guid? ParentCommentId { get; init; }

    public required string Content { get; init; }

    public string? LinkPreviewUrl { get; init; }

    public string? LinkPreviewTitle { get; init; }

    public string? LinkPreviewImageUrl { get; init; }

    public required UserSummaryReadModel Author { get; init; }

    public IReadOnlyCollection<PostMediaReadModel> Media { get; init; } = [];

    public PostPollSummaryReadModel? Poll { get; init; }

    public string? LocationPlaceId { get; init; }

    public string? LocationName { get; init; }

    public string? LocationCountry { get; init; }

    public double? LocationLatitude { get; init; }

    public double? LocationLongitude { get; init; }

    public int LikesCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public int RepliesCount { get; init; }

    public bool IsBookmarkedByCurrentUser { get; init; }

    public int RepostsCount { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }

    public int ViewsCount { get; init; }

    public DateTimeOffset? ActionAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
