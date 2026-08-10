using Threads.Application.DTOs.Users;

namespace Threads.Application.Interfaces.Users;

public interface IUserService
{
    Task<IReadOnlyCollection<UserShortResponse>> GetAllAsync(CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<IReadOnlyCollection<UserShortResponse>> SearchAsync(string query, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, Guid? currentUserId = null); 
    Task<UserResponse?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default, Guid? currentUserId = null);
    Task<UserResponse?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        UserFileUploadRequest? avatar = null,
        UserFileUploadRequest? banner = null,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
