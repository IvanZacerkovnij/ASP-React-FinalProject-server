namespace Threads.Application.DTOs.Media;

public sealed record MediaProcessingResult
{
    public int? Width { get; init; }

    public int? Height { get; init; }

    public double? DurationSeconds { get; init; }

    public string? ThumbnailFilePath { get; init; }
}
