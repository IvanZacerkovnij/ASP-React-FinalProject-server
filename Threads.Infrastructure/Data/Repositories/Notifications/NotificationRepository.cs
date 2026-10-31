using Microsoft.EntityFrameworkCore;
using Threads.Application.DTOs.Notifications;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Notifications;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Infrastructure.Data.Repositories.Notifications;

public sealed class NotificationRepository(ThreadsDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyCollection<NotificationReadModel>> GetByRecipientIdAsync(
        Guid recipientId, int limit, CursorPosition? cursor = null, CancellationToken cancellationToken = default)
    {
        var query = AvailableNotifications().Where(notification => notification.RecipientId == recipientId);
        if (cursor is not null)
        {
            query = query.Where(notification => EF.Functions.LessThan(
                ValueTuple.Create(notification.CreatedAt, notification.Id),
                ValueTuple.Create(cursor.CreatedAt, cursor.Id)));
        }

        return await query.OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(limit + 1)
            .Select(notification => new NotificationReadModel
            {
                Id = notification.Id,
                Type = notification.Type,
                Actor = new UserSummaryReadModel
                {
                    Id = notification.Actor.Id,
                    Username = notification.Actor.Username,
                    DisplayName = notification.Actor.DisplayName,
                    Bio = notification.Actor.Bio,
                    AvatarObjectKey = notification.Actor.AvatarObjectKey,
                    IsVerified = notification.Actor.IsVerified,
                    LocationName = notification.Actor.Location,
                    LocationPlaceId = notification.Actor.LocationPlaceId,
                    LocationCountry = notification.Actor.LocationCountry,
                    LocationLatitude = notification.Actor.LocationLatitude,
                    LocationLongitude = notification.Actor.LocationLongitude
                },
                SourceId = notification.SourceId,
                PostId = notification.PostId,
                CommentId = notification.CommentId,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            }).ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        return AvailableNotifications().CountAsync(
            notification => notification.RecipientId == recipientId && notification.ReadAt == null, cancellationToken);
    }

    public Task<Guid?> GetRecipientIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        return AvailableNotifications().Where(notification => notification.Id == notificationId)
            .Select(notification => (Guid?)notification.RecipientId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(
        Guid notificationId, Guid recipientId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        await AvailableNotifications()
            .Where(notification => notification.Id == notificationId && notification.RecipientId == recipientId && notification.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, readAt), cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid recipientId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        await AvailableNotifications().Where(notification => notification.RecipientId == recipientId && notification.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, readAt), cancellationToken);
    }

    private IQueryable<Notification> AvailableNotifications()
    {
        return dbContext.Notifications.AsNoTracking().Where(notification =>
            dbContext.Users.Any(user => user.Id == notification.RecipientId) &&
            dbContext.Users.Any(user => user.Id == notification.ActorId) &&
            (!notification.PostId.HasValue || dbContext.Posts.Any(post => post.Id == notification.PostId)) &&
            (!notification.CommentId.HasValue || dbContext.Comments.Any(comment => comment.Id == notification.CommentId)) &&
            (notification.Type != NotificationType.Quote || dbContext.Posts.Any(post => post.Id == notification.SourceId)));
    }
}
