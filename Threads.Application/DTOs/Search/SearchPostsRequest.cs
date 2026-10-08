using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Search;

[ValidSearchDateRange]
public sealed class SearchPostsRequest : CursorPageRequest
{
    [StringLength(100)]
    public string? Q { get; init; }

    [TrimmedAllowedValue("following")]
    public string? People { get; init; }

    [TrimmedAllowedValue("near")]
    public string? Location { get; init; }

    public string? ExactPhrase { get; init; }

    public string? AnyWords { get; init; }

    public string? ExcludeWords { get; init; }

    [StringLength(50)]
    public string? From { get; init; }

    [Range(0, int.MaxValue)]
    public int? MinReplies { get; init; }

    [Range(0, int.MaxValue)]
    public int? MinLikes { get; init; }

    [Range(0, int.MaxValue)]
    public int? MinReposts { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }

    public bool? HasMedia { get; init; }
}
