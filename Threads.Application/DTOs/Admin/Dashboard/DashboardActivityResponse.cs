namespace Threads.Application.DTOs.Admin;

public sealed class DashboardActivityResponse
{
    public IReadOnlyCollection<DashboardPointResponse> Posts { get; init; } = [];
    public IReadOnlyCollection<DashboardPointResponse> Comments { get; init; } = [];
}
