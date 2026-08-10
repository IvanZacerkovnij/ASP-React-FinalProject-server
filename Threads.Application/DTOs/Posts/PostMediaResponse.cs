namespace Threads.Application.DTOs.Posts;

public class PostMediaResponse
{
    public Guid Id { get; init; }

    public required string Url { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required string Type { get; init; }

    public long SizeInBytes { get; init; }

    public int SortOrder { get; init; }
}
