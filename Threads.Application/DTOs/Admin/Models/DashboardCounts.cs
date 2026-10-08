namespace Threads.Application.DTOs.Admin.Models;

public sealed record DashboardCounts(
    long Users,
    long ActiveUsers,
    long BlockedUsers,
    long PendingReports);
