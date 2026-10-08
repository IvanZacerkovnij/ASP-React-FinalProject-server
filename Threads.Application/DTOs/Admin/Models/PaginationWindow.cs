namespace Threads.Application.DTOs.Admin.Models;

public sealed record PaginationWindow(int Page, int TotalPages)
{
    public static PaginationWindow Calculate(int requestedPage, int total, int pageSize)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PaginationWindow(Math.Min(requestedPage, totalPages), totalPages);
    }
}
