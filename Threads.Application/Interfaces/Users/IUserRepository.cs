using Threads.Domain.Entities;

namespace Threads.Application.Interfaces.Users;

public interface IUserRepository
{
    Task<IReadOnlyCollection<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<User>> SearchAsync(string query, int take = 20, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteAsync(User user, CancellationToken cancellationToken = default);
}
