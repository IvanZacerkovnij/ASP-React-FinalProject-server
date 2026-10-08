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
    public async Task<PageResult<User>> GetUsersAsync(
        AdminUserCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.DeletedAt == null);

        if (criteria.Search is not null)
        {
            query = query.Where(user =>
                user.Username.ToLower().Contains(criteria.Search) ||
                user.Email.ToLower().Contains(criteria.Search) ||
                (user.DisplayName != null && user.DisplayName.ToLower().Contains(criteria.Search)));
        }

        if (criteria.IsBlocked.HasValue)
        {
            query = query.Where(user => user.IsActive == !criteria.IsBlocked.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var pagination = PaginationWindow.Calculate(
            criteria.Page,
            total,
            AdminPaginationContract.PageSize);
        query = criteria.Descending
            ? query.OrderByDescending(user => user.CreatedAt).ThenByDescending(user => user.Id)
            : query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id);

        var items = await query
            .Skip((pagination.Page - 1) * AdminPaginationContract.PageSize)
            .Take(AdminPaginationContract.PageSize)
            .Include(user => user.Posts.Where(post => post.DeletedAt == null))
            .Include(user => user.FollowingRelations)
            .Include(user => user.FollowerRelations)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PageResult<User>(items, pagination.Page, total, pagination.TotalPages);
    }

    public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.DeletedAt == null)
            .Include(user => user.Posts.Where(post => post.DeletedAt == null))
            .Include(user => user.FollowingRelations)
            .Include(user => user.FollowerRelations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<bool> SetUserBlockedAsync(
        Guid id,
        bool isBlocked,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => user.Id == id && user.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.IsActive, !isBlocked)
                .SetProperty(user => user.UpdatedAt, now), cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        if (isBlocked)
        {
            await RevokeRefreshTokensAsync(id, now, cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => user.Id == id && user.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.IsActive, false)
                .SetProperty(user => user.DeletedAt, now)
                .SetProperty(user => user.UpdatedAt, now), cancellationToken);

        if (affected == 0)
        {
            return false;
        }

        await RevokeRefreshTokensAsync(id, now, cancellationToken);
        return true;
    }

    private Task RevokeRefreshTokensAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        return _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, revokedAt), cancellationToken);
    }
}

