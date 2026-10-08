namespace Threads.Application.DTOs.Admin;

public sealed class DashboardTablesResponse
{
    public IReadOnlyCollection<AdminUserResponse> LatestUsers { get; init; } = [];
    public IReadOnlyCollection<ReportRowResponse> LatestReports { get; init; } = [];
}
