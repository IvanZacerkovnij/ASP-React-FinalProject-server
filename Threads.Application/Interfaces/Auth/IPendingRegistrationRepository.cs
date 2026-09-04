using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Auth;

public interface IPendingRegistrationRepository
{
    Task<PendingRegistration?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<PendingRegistration?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken = default);
    Task UpdateAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken = default);
    Task DeleteAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken = default);
}
