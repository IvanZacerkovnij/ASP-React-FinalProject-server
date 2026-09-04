using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class View : BaseEntity
{
    public Guid? PostId { get; set; }

    public Post? Post { get; set; }

    public Guid? CommentId { get; set; }

    public Comment? Comment { get; set; }

    public Guid ViewerId { get; set; }

    public User Viewer { get; set; } = null!;
}
