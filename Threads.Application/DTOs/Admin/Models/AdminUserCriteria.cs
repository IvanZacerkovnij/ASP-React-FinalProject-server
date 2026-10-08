namespace Threads.Application.DTOs.Admin.Models;

public sealed class AdminUserCriteria
{
    public int Page { get; init; }
    public string? Search { get; init; }
    public bool? IsBlocked { get; init; }
    public bool Descending { get; init; }
}
