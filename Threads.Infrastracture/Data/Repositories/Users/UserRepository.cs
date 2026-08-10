using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Users;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.Users;

public class UserRepository : IUserRepository
{
    private readonly ThreadsDbContext _dbContext;

    public UserRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await BuildUserQuery(trackChanges: false)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> SearchAsync(
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await BuildUserQuery(trackChanges: false)
            .Where(user =>
                EF.Functions.ILike(user.Username, $"%{query}%") ||
                (user.DisplayName != null && EF.Functions.ILike(user.DisplayName, $"%{query}%")) ||
                (user.Location != null && EF.Functions.ILike(user.Location, $"%{query}%")) ||
                (user.LocationCountry != null && EF.Functions.ILike(user.LocationCountry, $"%{query}%")))
            .OrderBy(user => user.Username)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BuildUserQuery(trackChanges: true)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
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

        return await BuildUserQuery(trackChanges: false)
            .FirstOrDefaultAsync(user => user.Username.ToLower() == normalizedUsername, cancellationToken);
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

    private IQueryable<User> BuildUserQuery(bool trackChanges)
    {
        var query = trackChanges
            ? _dbContext.Users.AsQueryable()
            : _dbContext.Users.AsNoTracking();

        return query
            .AsSplitQuery()
            .Include(user => user.Posts)
            .Include(user => user.FollowingRelations)
            .Include(user => user.FollowerRelations);
    }
}
