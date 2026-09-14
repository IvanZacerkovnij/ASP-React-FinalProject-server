namespace Threads.Application.DTOs.Pagination;

public sealed record CursorPosition(DateTimeOffset CreatedAt, Guid Id);
