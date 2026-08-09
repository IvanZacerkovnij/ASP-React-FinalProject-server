using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Poll : BaseEntity
{
    public Guid PostId { get; set; }

    public Post Post { get; set; } = null!;

    public DateTimeOffset? EndsAt { get; set; }

    public ICollection<PollOption> Options { get; set; } =
        new List<PollOption>();

    public ICollection<PollVote> Votes { get; set; } =
        new List<PollVote>();
}
