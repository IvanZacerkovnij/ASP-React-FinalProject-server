using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Search;

public sealed class SearchUsersRequest : CursorPageRequest
{
    [StringLength(100)]
    public string? Q { get; init; }

    [TrimmedAllowedValue("following")]
    public string? People { get; init; }

    [TrimmedAllowedValue("near")]
    public string? Location { get; init; }
}
