using Microsoft.EntityFrameworkCore;
using Threads.Application.Interfaces.Auth;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Repositories.PendingRegistrations;

public class PendingRegistrationRepository : IPendingRegistrationRepository
{
    private readonly ThreadsDbContext _dbContext;

    public PendingRegistrationRepository(ThreadsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PendingRegistration?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

        return await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(
                registration => registration.Email.ToLower() == normalizedEmail,
                cancellationToken);
    }

    public async Task<PendingRegistration?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = username.ToLowerInvariant();

        return await _dbContext.PendingRegistrations
            .FirstOrDefaultAsync(
                registration => registration.Username.ToLower() == normalizedUsername,
                cancellationToken);
    }

    public async Task AddAsync(
        PendingRegistration pendingRegistration,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PendingRegistrations.AddAsync(pendingRegistration, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PendingRegistration pendingRegistration,
        CancellationToken cancellationToken = default)
    {
        _dbContext.PendingRegistrations.Update(pendingRegistration);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        PendingRegistration pendingRegistration,
        CancellationToken cancellationToken = default)
    {
        _dbContext.PendingRegistrations.Remove(pendingRegistration);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
