namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostReadModel
{
    public Guid Id { get; init; }

    public string? Content { get; init; }

    public Guid AuthorId { get; init; }

    public IReadOnlyCollection<PostMediaReadModel> Media { get; init; } = [];

    public PostPollReadModel? Poll { get; init; }

    public string? LocationPlaceId { get; init; }

    public string? LocationName { get; init; }

    public string? LocationCountry { get; init; }

    public double? LocationLatitude { get; init; }

    public double? LocationLongitude { get; init; }

    public string? EmbedUrl { get; init; }

    public string? EmbedTitle { get; init; }

    public string? EmbedDescription { get; init; }

    public string? EmbedThumbnailUrl { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
