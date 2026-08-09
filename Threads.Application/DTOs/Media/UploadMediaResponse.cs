namespace Threads.Application.DTOs.Media;

public class UploadMediaResponse
{
    public Guid Id { get; init; }

    public required string StorageKey { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public long SizeInBytes { get; init; }

    public string Type { get; init; } = null!;
}
