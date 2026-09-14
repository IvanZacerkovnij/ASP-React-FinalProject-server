using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Pagination;

public sealed class CursorPageRequest
{
    [Range(1, 50)]
    public int Limit { get; init; } = 20;

    public string? Cursor { get; init; }
}
