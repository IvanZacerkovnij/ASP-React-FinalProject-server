namespace Threads.Application.DTOs.Admin;

public sealed class ModerationQuery
{
    public int Page { get; init; } = AdminPaginationContract.DefaultPage;
    public string? Search { get; init; }
    public string? Type { get; init; }
    public string? Source { get; init; }
    public string? Status { get; init; }
    public string? Decision { get; init; }
    public string? Sort { get; init; }
}
