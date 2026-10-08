namespace Threads.Application.DTOs.Admin;

public sealed class AdminPageResponse<T>
{
    public IReadOnlyCollection<T> Items { get; init; } = [];
    public required PaginationResponse Pagination { get; init; }
}
