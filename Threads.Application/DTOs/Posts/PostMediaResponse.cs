namespace Threads.Application.DTOs.Posts;

public class PostMediaResponse
{
    public Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Url { get; init; }

    public string? ThumbnailUrl { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public double? Duration { get; init; }

    public long Size { get; init; }

    public required string MimeType { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public long SizeInBytes { get; init; }

    public int SortOrder { get; init; }
}
