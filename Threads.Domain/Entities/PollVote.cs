using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class PollVote : BaseEntity
{
    public Guid PollId { get; set; }

    public Poll Poll { get; set; } = null!;

    public Guid PollOptionId { get; set; }

    public PollOption PollOption { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}
