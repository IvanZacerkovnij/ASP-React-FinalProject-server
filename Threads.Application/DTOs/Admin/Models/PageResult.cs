namespace Threads.Application.DTOs.Admin.Models;

public sealed record PageResult<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int Total,
    int TotalPages);
