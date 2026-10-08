namespace Threads.Application.DTOs.Admin;

public sealed class DashboardPointResponse
{
    public required string Date { get; init; }
    public long Value { get; init; }
}
