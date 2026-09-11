using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostMediaReadModel
{
    public Guid Id { get; init; }

    public required string StorageKey { get; init; }

    public string? ThumbnailStorageKey { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public MediaType Type { get; init; }

    public long SizeInBytes { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public double? DurationSeconds { get; init; }

    public int SortOrder { get; init; }
}
