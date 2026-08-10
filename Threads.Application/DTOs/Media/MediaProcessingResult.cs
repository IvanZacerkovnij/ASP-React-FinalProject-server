namespace Threads.Application.DTOs.Media;

public sealed record MediaProcessingResult
{
    public int? Width { get; init; }

    public int? Height { get; init; }

    public double? DurationSeconds { get; init; }

    public string? ProcessedFilePath { get; init; }

    public string? OutputContentType { get; init; }

    public string? OutputFileName { get; init; }

    public long? OutputSizeInBytes { get; init; }

    public string? ThumbnailFilePath { get; init; }
}
