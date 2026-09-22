using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.Users;

public class UserRepository : IUserRepository
{
    private readonly ThreadsDbContext _dbContext;

    public UserRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<UserSummaryReadModel>> SearchAsync(
        string query,
        int limit,
        TextCursorPosition? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var users = _dbContext.Users
            .AsNoTracking()
            .Where(user =>
                EF.Functions.ILike(user.Username, $"%{query}%") ||
                (user.DisplayName != null && EF.Functions.ILike(user.DisplayName, $"%{query}%")) ||
                (user.Location != null && EF.Functions.ILike(user.Location, $"%{query}%")) ||
                (user.LocationCountry != null && EF.Functions.ILike(user.LocationCountry, $"%{query}%")));

        if (cursor is not null)
        {
            users = users.Where(user => EF.Functions.GreaterThan(
                ValueTuple.Create(user.Username, user.Id),
                ValueTuple.Create(cursor.Value, cursor.Id)));
        }

        return await users
            .OrderBy(user => user.Username)
            .ThenBy(user => user.Id)
            .Take(limit + 1)
            .Select(user => new UserSummaryReadModel
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                LocationPlaceId = user.LocationPlaceId,
                LocationName = user.Location,
                LocationCountry = user.LocationCountry,
                LocationLatitude = user.LocationLatitude,
                LocationLongitude = user.LocationLongitude,
                AvatarObjectKey = user.AvatarObjectKey,
                IsVerified = user.IsVerified
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BuildUserWithRelationsQuery()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<UserProfileReadModel?> GetProfileByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserProfileReadModel
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                DateOfBirth = user.DateOfBirth,
                LocationPlaceId = user.LocationPlaceId,
                LocationName = user.Location,
                LocationCountry = user.LocationCountry,
                LocationLatitude = user.LocationLatitude,
                LocationLongitude = user.LocationLongitude,
                AvatarObjectKey = user.AvatarObjectKey,
                BannerObjectKey = user.BannerObjectKey,
                FollowersCount = user.FollowerRelations.Count,
                FollowingCount = user.FollowingRelations.Count,
                PostsCount = user.Posts.Count,
                IsVerified = user.IsVerified,
                CreatedAt = user.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLower();

        return await _dbContext.Users
            .FirstOrDefaultAsync(user => user.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.ToLower();

        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Username.ToLower() == normalizedUsername, cancellationToken);
    }

    public async Task<Guid?> GetIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.ToLowerInvariant();

        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Username == normalizedUsername)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<User> BuildUserWithRelationsQuery()
    {
        return _dbContext.Users
            .AsSplitQuery()
            .Include(user => user.Posts)
            .Include(user => user.FollowingRelations)
            .Include(user => user.FollowerRelations);
    }
}
