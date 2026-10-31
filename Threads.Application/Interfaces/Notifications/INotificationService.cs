using Threads.Application.DTOs.Notifications;
using Threads.Application.DTOs.Pagination;

namespace Threads.Application.Interfaces.Notifications;

public interface INotificationService
{
    Task<CursorPageResponse<NotificationResponse>> GetAsync(
        Guid currentUserId, CursorPageRequest pagination, CancellationToken cancellationToken = default);

    Task<UnreadNotificationCountResponse> GetUnreadCountAsync(Guid currentUserId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid currentUserId, Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid currentUserId, CancellationToken cancellationToken = default);
}
