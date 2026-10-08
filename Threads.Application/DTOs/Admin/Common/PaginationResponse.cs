namespace Threads.Application.DTOs.Admin;

public sealed class PaginationResponse
{
    public int Page { get; init; }
    public int Total { get; init; }
    public int TotalPages { get; init; }
}
