using Threads.Application.DTOs.Users;
using Threads.Domain.Enums;

namespace Threads.Application.DTOs.Notifications;

public sealed class NotificationReadModel
{
    public Guid Id { get; init; }
    public NotificationType Type { get; init; }
    public required UserSummaryReadModel Actor { get; init; }
    public Guid SourceId { get; init; }
    public Guid? PostId { get; init; }
    public Guid? CommentId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReadAt { get; init; }
}
