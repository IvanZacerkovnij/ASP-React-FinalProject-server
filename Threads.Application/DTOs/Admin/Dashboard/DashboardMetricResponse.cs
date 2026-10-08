namespace Threads.Application.DTOs.Admin;

public sealed class DashboardMetricResponse
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public long? Value { get; init; }
}
