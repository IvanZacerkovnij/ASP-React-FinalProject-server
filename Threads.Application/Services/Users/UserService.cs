using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Users;

namespace Threads.Application.Services.Users;

public sealed class UserService : IUserService
{
    private readonly UserQueryService _userQueryService;
    private readonly UserProfileService _userProfileService;
    private readonly UserDeletionService _userDeletionService;

    public UserService(
        UserQueryService userQueryService,
        UserProfileService userProfileService,
        UserDeletionService userDeletionService)
    {
        _userQueryService = userQueryService;
        _userProfileService = userProfileService;
        _userDeletionService = userDeletionService;
    }

    public Task<CursorPageResponse<UserShortResponse>> SearchAsync(
        string query,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _userQueryService.SearchAsync(query, pagination, cancellationToken, currentUserId);
    }

    public Task<UserResponse?> GetMeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _userQueryService.GetMeAsync(id, cancellationToken);
    }

    public Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _userQueryService.GetByIdAsync(id, cancellationToken, currentUserId);
    }

    public Task<UserResponse?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        return _userQueryService.GetByUsernameAsync(username, cancellationToken, currentUserId);
    }

    public Task<UserResponse?> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        UserFileUploadRequest? avatar = null,
        UserFileUploadRequest? banner = null,
        CancellationToken cancellationToken = default)
    {
        return _userProfileService.UpdateAsync(id, request, avatar, banner, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _userDeletionService.DeleteAsync(id, cancellationToken);
    }
}
