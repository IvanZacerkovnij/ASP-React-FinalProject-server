using Threads.Application.DTOs.Admin;
using Threads.Domain.Enums;

namespace Threads.Application.Interfaces.Admin;

public interface IAdministrationService
{
    Task CreateReportAsync(ReportTargetType targetType, Guid targetId, Guid reporterId, CreateReportRequest request, CancellationToken cancellationToken = default);
    Task<AdminPageResponse<AdminUserResponse>> GetUsersAsync(AdminUsersQuery query, CancellationToken cancellationToken = default);
    Task<AdminUserResponse?> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetUserBlockedAsync(Guid id, Guid adminId, bool isBlocked, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid id, Guid adminId, CancellationToken cancellationToken = default);
    Task<AdminPageResponse<ReportResponse>> GetReportsAsync(ModerationQuery query, CancellationToken cancellationToken = default);
    Task<ReportResponse?> GetReportAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReportResponse> ResolveReportAsync(Guid id, Guid adminId, ResolveReportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DashboardMetricResponse>> GetMetricsAsync(CancellationToken cancellationToken = default);
    Task<DashboardChartsResponse> GetChartsAsync(string period, CancellationToken cancellationToken = default);
    Task<DashboardTablesResponse> GetTablesAsync(int limit, CancellationToken cancellationToken = default);
}
