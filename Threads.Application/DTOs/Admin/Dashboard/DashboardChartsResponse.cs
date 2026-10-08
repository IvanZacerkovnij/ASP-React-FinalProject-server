namespace Threads.Application.DTOs.Admin;

public sealed class DashboardChartsResponse
{
    public IReadOnlyDictionary<string, IReadOnlyCollection<DashboardPointResponse>> Audience { get; init; } =
        new Dictionary<string, IReadOnlyCollection<DashboardPointResponse>>();

    public IReadOnlyDictionary<string, DashboardActivityResponse> Activity { get; init; } =
        new Dictionary<string, DashboardActivityResponse>();
}
