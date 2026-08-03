using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Follow : BaseEntity
{
    public Guid FollowerId { get; set; }

    public User Follower { get; set; } = null!;

    public Guid FollowingId { get; set; }

    public User Following { get; set; } = null!;
}