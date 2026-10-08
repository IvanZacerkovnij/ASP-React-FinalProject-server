using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Threads.Application.DTOs.Admin.Models;

namespace Threads.Application.Interfaces.Admin;

public interface IAdministrationRepository
{
    Task<ReportTargetInfo> GetTargetInfoAsync(ReportTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);
    Task AddReportAsync(Report report, CancellationToken cancellationToken = default);
    Task<PageResult<User>> GetUsersAsync(AdminUserCriteria criteria, CancellationToken cancellationToken = default);
    Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetUserBlockedAsync(Guid id, bool isBlocked, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PageResult<Report>> GetReportsAsync(ReportCriteria criteria, CancellationToken cancellationToken = default);
    Task<Report?> GetReportAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ResolveReportResult> ResolveReportAsync(Guid id, Guid adminId, ReportDecision decision, CancellationToken cancellationToken = default);
    Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default);
    Task<long> GetUsersCreatedBeforeAsync(DateTimeOffset start, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DailyCount>> GetUsersCreatedByDayAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DailyCount>> GetPostsCreatedByDayAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DailyCount>> GetCommentsCreatedByDayAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<User>> GetLatestUsersAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReportGroup>> GetLatestReportGroupsAsync(int limit, CancellationToken cancellationToken = default);
}
