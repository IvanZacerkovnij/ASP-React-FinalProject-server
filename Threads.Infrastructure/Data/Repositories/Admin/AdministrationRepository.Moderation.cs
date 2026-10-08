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
    public async Task<PageResult<Report>> GetReportsAsync(
        ReportCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyReportCriteria(_dbContext.Reports.IgnoreQueryFilters().AsNoTracking(), criteria);
        var total = await query.CountAsync(cancellationToken);
        var pagination = PaginationWindow.Calculate(
            criteria.Page,
            total,
            AdminPaginationContract.PageSize);
        query = criteria.Descending
            ? query.OrderByDescending(report => report.CreatedAt).ThenByDescending(report => report.Id)
            : query.OrderBy(report => report.CreatedAt).ThenBy(report => report.Id);

        var items = await query
            .Skip((pagination.Page - 1) * AdminPaginationContract.PageSize)
            .Take(AdminPaginationContract.PageSize)
            .Include(report => report.Reporter)
            .Include(report => report.ResolvedBy)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PageResult<Report>(items, pagination.Page, total, pagination.TotalPages);
    }

    public Task<Report?> GetReportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Reports
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(report => report.Reporter)
            .Include(report => report.ResolvedBy)
            .FirstOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public async Task<ResolveReportResult> ResolveReportAsync(
        Guid id,
        Guid adminId,
        ReportDecision decision,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var report = await _dbContext.Reports
            .FromSqlInterpolated($"SELECT * FROM \"Reports\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (report is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ResolveReportResult(ResolveReportOutcome.NotFound, null);
        }

        if (report.Status == ReportStatus.Resolved)
        {
            await transaction.CommitAsync(cancellationToken);
            if (report.Decision != decision)
            {
                return new ResolveReportResult(ResolveReportOutcome.Conflict, report);
            }

            return new ResolveReportResult(
                ResolveReportOutcome.Idempotent,
                await ReloadReportAsync(id, cancellationToken));
        }

        var targetAvailable = await IsTargetAvailableAsync(report.TargetType, report.TargetId, cancellationToken);
        if (!targetAvailable && decision != ReportDecision.Kept)
        {
            var wasPreviouslyDeleted = decision == ReportDecision.Deleted &&
                await _dbContext.Reports.AsNoTracking().AnyAsync(other =>
                    other.Id != report.Id &&
                    other.TargetType == report.TargetType &&
                    other.TargetId == report.TargetId &&
                    other.Status == ReportStatus.Resolved &&
                    other.Decision == ReportDecision.Deleted,
                    cancellationToken);

            if (!wasPreviouslyDeleted)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new ResolveReportResult(ResolveReportOutcome.TargetUnavailable, report);
            }
        }

        if (targetAvailable)
        {
            await ApplyDecisionAsync(report.TargetType, report.TargetId, decision, cancellationToken);
        }

        report.Status = ReportStatus.Resolved;
        report.Decision = decision;
        report.ResolvedAt = DateTimeOffset.UtcNow;
        report.ResolvedById = adminId;
        report.UpdatedAt = report.ResolvedAt;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResolveReportResult(
            ResolveReportOutcome.Resolved,
            await ReloadReportAsync(id, cancellationToken));
    }

    private IQueryable<Report> ApplyReportCriteria(IQueryable<Report> query, ReportCriteria criteria)
    {
        if (criteria.Type.HasValue) query = query.Where(report => report.TargetType == criteria.Type.Value);
        if (criteria.Source.HasValue) query = query.Where(report => report.Source == criteria.Source.Value);
        if (criteria.Status.HasValue) query = query.Where(report => report.Status == criteria.Status.Value);
        if (criteria.Decision.HasValue) query = query.Where(report => report.Decision == criteria.Decision.Value);

        if (criteria.Search is not null)
        {
            var search = criteria.Search;
            query = query.Where(report =>
                report.TargetId.ToString().ToLower().Contains(search) ||
                report.Reason.ToLower().Contains(search) ||
                (report.Reporter != null && report.Reporter.Username.ToLower().Contains(search)) ||
                (report.TargetType == ReportTargetType.Posts && _dbContext.Posts.IgnoreQueryFilters().Any(post =>
                    post.Id == report.TargetId && post.Content != null && post.Content.ToLower().Contains(search))) ||
                (report.TargetType == ReportTargetType.Comments && _dbContext.Comments.IgnoreQueryFilters().Any(comment =>
                    comment.Id == report.TargetId && comment.Content.ToLower().Contains(search))) ||
                (report.TargetType == ReportTargetType.Users && _dbContext.Users.IgnoreQueryFilters().Any(user =>
                    user.Id == report.TargetId &&
                    (user.Username.ToLower().Contains(search) ||
                     user.Email.ToLower().Contains(search) ||
                     (user.DisplayName != null && user.DisplayName.ToLower().Contains(search))))));
        }

        return query;
    }

    private async Task<bool> IsTargetAvailableAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        return targetType switch
        {
            ReportTargetType.Posts => await _dbContext.Posts.AnyAsync(post => post.Id == targetId, cancellationToken),
            ReportTargetType.Comments => await _dbContext.Comments.AnyAsync(comment => comment.Id == targetId, cancellationToken),
            ReportTargetType.Users => await _dbContext.Users.IgnoreQueryFilters().AnyAsync(
                user => user.Id == targetId && user.DeletedAt == null,
                cancellationToken),
            _ => false
        };
    }

    private async Task ApplyDecisionAsync(
        ReportTargetType targetType,
        Guid targetId,
        ReportDecision decision,
        CancellationToken cancellationToken)
    {
        if (decision == ReportDecision.Kept)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (decision == ReportDecision.Blocked && targetType == ReportTargetType.Users)
        {
            await _dbContext.Users.IgnoreQueryFilters()
                .Where(user => user.Id == targetId && user.DeletedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(user => user.IsActive, false)
                    .SetProperty(user => user.UpdatedAt, now), cancellationToken);
            await RevokeRefreshTokensAsync(targetId, now, cancellationToken);
            return;
        }

        if (decision != ReportDecision.Deleted)
        {
            return;
        }

        switch (targetType)
        {
            case ReportTargetType.Posts:
                await _dbContext.Posts.IgnoreQueryFilters()
                    .Where(post => post.Id == targetId && post.DeletedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(post => post.DeletedAt, now)
                        .SetProperty(post => post.UpdatedAt, now), cancellationToken);
                break;
            case ReportTargetType.Comments:
                await SoftDeleteCommentTreeAsync(targetId, now, cancellationToken);
                break;
            case ReportTargetType.Users:
                await _dbContext.Users.IgnoreQueryFilters()
                    .Where(user => user.Id == targetId && user.DeletedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(user => user.IsActive, false)
                        .SetProperty(user => user.DeletedAt, now)
                        .SetProperty(user => user.UpdatedAt, now), cancellationToken);
                await RevokeRefreshTokensAsync(targetId, now, cancellationToken);
                break;
        }
    }

    private Task SoftDeleteCommentTreeAsync(
        Guid id,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        return _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH RECURSIVE descendants AS (
                SELECT "Id" FROM "Comments" WHERE "Id" = {id}
                UNION ALL
                SELECT child."Id"
                FROM "Comments" child
                INNER JOIN descendants parent ON child."ParentCommentId" = parent."Id"
            )
            UPDATE "Comments"
            SET "DeletedAt" = {deletedAt}, "UpdatedAt" = {deletedAt}
            WHERE "Id" IN (SELECT "Id" FROM descendants)
              AND "DeletedAt" IS NULL
            """,
            cancellationToken);
    }

    private async Task<Report?> ReloadReportAsync(Guid id, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();
        return await _dbContext.Reports.IgnoreQueryFilters().AsNoTracking()
            .Include(report => report.Reporter)
            .Include(report => report.ResolvedBy)
            .FirstOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

}
