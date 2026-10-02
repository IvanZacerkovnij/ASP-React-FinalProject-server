using Microsoft.EntityFrameworkCore;
using Npgsql;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Repositories.PendingRegistrations;

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

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  {
                      SqlState: PostgresErrorCodes.UniqueViolation,
                      ConstraintName: "IX_PendingRegistrations_Email" or "IX_PendingRegistrations_Username"
                  })
        {
            _dbContext.Entry(pendingRegistration).State = EntityState.Detached;
            throw new ConflictException("Pending registration with this email or username already exists.");
        }
    }

    public async Task UpdateAsync(
        PendingRegistration pendingRegistration,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(pendingRegistration).State == EntityState.Detached)
        {
            _dbContext.PendingRegistrations.Update(pendingRegistration);
        }

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
