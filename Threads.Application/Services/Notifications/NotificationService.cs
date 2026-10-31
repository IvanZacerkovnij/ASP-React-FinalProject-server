using Threads.Application.DTOs.Notifications;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Notifications;
using Threads.Application.Services.Common;
using Threads.Application.Services.Users;

namespace Threads.Application.Services.Notifications;

public sealed class NotificationService(
    INotificationRepository repository,
    UserResponseFactory userResponseFactory,
    TimeProvider timeProvider) : INotificationService
{
    public async Task<CursorPageResponse<NotificationResponse>> GetAsync(
        Guid currentUserId, CursorPageRequest pagination, CancellationToken cancellationToken = default)
    {
        RequestValidator.Validate(pagination);
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var notifications = await repository.GetByRecipientIdAsync(currentUserId, pagination.Limit, cursor, cancellationToken);
        var hasMore = notifications.Count > pagination.Limit;
        var page = notifications.Take(pagination.Limit).ToList();

        return new CursorPageResponse<NotificationResponse>
        {
            Items = page.Select(notification => new NotificationResponse
            {
                Id = notification.Id,
                Type = notification.Type,
                Actor = userResponseFactory.CreateShort(notification.Actor),
                SourceId = notification.SourceId,
                PostId = notification.PostId,
                CommentId = notification.CommentId,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            }).ToArray(),
            HasMore = hasMore,
            NextCursor = hasMore ? CursorCodec.Encode(page[^1].CreatedAt, page[^1].Id) : null
        };
    }

    public async Task<UnreadNotificationCountResponse> GetUnreadCountAsync(
        Guid currentUserId, CancellationToken cancellationToken = default)
    {
        return new UnreadNotificationCountResponse
        {
            UnreadCount = await repository.GetUnreadCountAsync(currentUserId, cancellationToken)
        };
    }

    public async Task MarkAsReadAsync(Guid currentUserId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var recipientId = await repository.GetRecipientIdAsync(notificationId, cancellationToken);
        if (recipientId != currentUserId)
        {
            throw new NotFoundException("Notification was not found.");
        }

        await repository.MarkAsReadAsync(notificationId, currentUserId, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task MarkAllAsReadAsync(Guid currentUserId, CancellationToken cancellationToken = default)
    {
        return repository.MarkAllAsReadAsync(currentUserId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
