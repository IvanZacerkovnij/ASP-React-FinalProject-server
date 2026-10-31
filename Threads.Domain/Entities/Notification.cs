using Threads.Domain.Common;
using Threads.Domain.Enums;

namespace Threads.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    public Guid ActorId { get; set; }
    public User Actor { get; set; } = null!;

    public NotificationType Type { get; set; }

    public Guid SourceId { get; set; }

    public Guid? PostId { get; set; }
    public Post? Post { get; set; }

    public Guid? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}
