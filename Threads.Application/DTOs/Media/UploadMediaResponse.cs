namespace Threads.Application.DTOs.Media;

public class UploadMediaResponse
{
    public Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Url { get; init; }

    public string? ThumbnailUrl { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public double? Duration { get; init; }

    public required string MimeType { get; init; }

    public required string StorageKey { get; init; }

    public required string FileName { get; init; }

    public long SizeInBytes { get; init; }
}
