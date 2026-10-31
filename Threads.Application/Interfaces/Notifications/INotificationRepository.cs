using Threads.Application.DTOs.Notifications;
using Threads.Application.DTOs.Pagination;

namespace Threads.Application.Interfaces.Notifications;

public interface INotificationRepository
{
    Task<IReadOnlyCollection<NotificationReadModel>> GetByRecipientIdAsync(
        Guid recipientId, int limit, CursorPosition? cursor = null, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task<Guid?> GetRecipientIdAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid notificationId, Guid recipientId, DateTimeOffset readAt, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid recipientId, DateTimeOffset readAt, CancellationToken cancellationToken = default);
}
