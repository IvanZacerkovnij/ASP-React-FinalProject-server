namespace Threads.Domain.Models.Versions;

public sealed class LinkPreviewVersionSnapshot
{
    public required string Url { get; init; }
    
    public string? Description { get; init; }

    public string? Title { get; init; }
    
    public string? ThumbnailUrl { get; init; }
}