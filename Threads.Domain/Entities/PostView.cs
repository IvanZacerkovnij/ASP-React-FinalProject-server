using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class PostView : BaseEntity
{
    public Guid PostId { get; set; }

    public Post Post { get; set; } = null!;

    public Guid ViewerId { get; set; }

    public User Viewer { get; set; } = null!;
}
