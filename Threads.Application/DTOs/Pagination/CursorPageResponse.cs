namespace Threads.Application.DTOs.Pagination;

public sealed class CursorPageResponse<T>
{
    public required IReadOnlyCollection<T> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
