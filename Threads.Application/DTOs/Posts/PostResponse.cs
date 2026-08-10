using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Polls;

namespace Threads.Application.DTOs.Posts;

public class PostResponse
{
    public Guid Id { get; init; }

    public required string Content { get; init; }

    public required UserShortResponse Author { get; init; }

    public IReadOnlyCollection<PostMediaResponse> Media { get; init; } = [];

    public IReadOnlyCollection<string> MediaUrls { get; init; } = [];

    public PollResponse? Poll { get; init; }

    public PostLocationResponse? Location { get; init; }

    public PostEmbedResponse? Embed { get; init; }

    public int LikesCount { get; init; }

    public int CommentsCount { get; init; }

    public int RepostsCount { get; init; }

    public int ViewsCount { get; init; }

    public bool IsLikedByCurrentUser { get; init; }

    public bool IsRepostedByCurrentUser { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
