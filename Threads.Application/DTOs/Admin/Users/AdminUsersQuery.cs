namespace Threads.Application.DTOs.Admin;

public sealed class AdminUsersQuery
{
    public int Page { get; init; } = AdminPaginationContract.DefaultPage;
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Sort { get; init; }
}
