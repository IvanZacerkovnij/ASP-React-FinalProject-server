using System.Data;
using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Admin;
using Threads.Application.DTOs.Admin.Models;
using Threads.Application.Interfaces.Admin;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Infrastructure.Data.Repositories.Admin;

public sealed partial class AdministrationRepository
{
    public async Task<DashboardCounts> GetDashboardCountsAsync(CancellationToken cancellationToken = default)
    {
        var users = _dbContext.Users.IgnoreQueryFilters().AsNoTracking().Where(user => user.DeletedAt == null);
        var total = await users.LongCountAsync(cancellationToken);
        var active = await users.LongCountAsync(user => user.IsActive, cancellationToken);
        var blocked = await users.LongCountAsync(user => !user.IsActive, cancellationToken);
        var pending = await _dbContext.Reports.LongCountAsync(
            report => report.Status == ReportStatus.Pending,
            cancellationToken);
        return new DashboardCounts(total, active, blocked, pending);
    }

    public Task<long> GetUsersCreatedBeforeAsync(
        DateTimeOffset start,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.IgnoreQueryFilters().AsNoTracking().LongCountAsync(
            user => user.DeletedAt == null && user.CreatedAt < start,
            cancellationToken);
    }

    public Task<IReadOnlyCollection<DailyCount>> GetUsersCreatedByDayAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default) =>
        GetDailyCountsAsync(
            _dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.DeletedAt == null)
                .Select(user => user.CreatedAt),
            start,
            end,
            cancellationToken);

    public Task<IReadOnlyCollection<DailyCount>> GetPostsCreatedByDayAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default) =>
        GetDailyCountsAsync(
            _dbContext.Posts.AsNoTracking().Select(post => post.CreatedAt),
            start,
            end,
            cancellationToken);

    public Task<IReadOnlyCollection<DailyCount>> GetCommentsCreatedByDayAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default) =>
        GetDailyCountsAsync(
            _dbContext.Comments.AsNoTracking().Select(comment => comment.CreatedAt),
            start,
            end,
            cancellationToken);

    public async Task<IReadOnlyCollection<User>> GetLatestUsersAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.DeletedAt == null)
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Take(limit)
            .Include(user => user.Posts.Where(post => post.DeletedAt == null))
            .Include(user => user.FollowingRelations)
            .Include(user => user.FollowerRelations)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReportGroup>> GetLatestReportGroupsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var groups = await _dbContext.Reports
            .AsNoTracking()
            .GroupBy(report => new { report.TargetType, report.TargetId })
            .Select(group => new
            {
                group.Key.TargetType,
                group.Key.TargetId,
                Count = group.Count(),
                LatestSignal = group.Max(report => report.CreatedAt)
            })
            .OrderByDescending(group => group.LatestSignal)
            .ThenByDescending(group => group.TargetId)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return groups
            .Select(group => new ReportGroup(
                group.TargetType,
                group.TargetId,
                group.Count,
                group.LatestSignal))
            .ToArray();
    }

    private static async Task<IReadOnlyCollection<DailyCount>> GetDailyCountsAsync(
        IQueryable<DateTimeOffset> timestamps,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var counts = await timestamps
            .Where(timestamp => timestamp >= start && timestamp < end)
            .GroupBy(timestamp => timestamp.Date)
            .Select(group => new { Date = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);

        return counts
            .Select(item => new DailyCount(DateOnly.FromDateTime(item.Date), item.Count))
            .ToArray();
    }
}
