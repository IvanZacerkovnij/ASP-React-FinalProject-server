using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Quotes;

namespace Threads.Application.DTOs.Posts.Responses;

public class PostResponse
{
    public Guid Id { get; init; }

    public Guid VersionId { get; init; }

    public required string Content { get; init; }

    public required UserShortResponse Author { get; init; }

    public IReadOnlyCollection<MediaAttachmentResponse> Media { get; init; } = [];

    public PollResponse? Poll { get; init; }

    public PostLocationResponse? Location { get; init; }

    public LinkPreviewResponse? LinkPreview { get; init; }

    public QuoteResponse? Quote { get; set; }

    public int LikesCount { get; init; }

    public int CommentsCount { get; init; }

    public int RepostsCount { get; init; }
    
    public int BookmarksCount { get; init; }

    public int ViewsCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }
    
    public bool IsBookmarkedByCurrentUser { get; init; }

    public DateTimeOffset? ActionAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
