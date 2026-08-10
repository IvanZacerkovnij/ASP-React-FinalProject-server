using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Repost : BaseEntity
{
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid PostId { get; set; }

    public Post Post { get; set; } = null!;
}
