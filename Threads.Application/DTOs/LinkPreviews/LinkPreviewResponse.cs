namespace Threads.Application.DTOs.LinkPreviews;

public sealed class LinkPreviewResponse
{
    public required string Id { get; init; }

    public required string Url { get; init; }

    public required string Domain { get; init; }

    public string? Title { get; init; }

    public string? ImageUrl { get; init; }
}
