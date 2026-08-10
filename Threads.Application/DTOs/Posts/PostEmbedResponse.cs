namespace Threads.Application.DTOs.Posts;

public class PostEmbedResponse
{
    public required string Url { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? ThumbnailUrl { get; init; }
}
