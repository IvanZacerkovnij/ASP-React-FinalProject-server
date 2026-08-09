using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class PollOption : BaseEntity
{
    public Guid PollId { get; set; }

    public Poll Poll { get; set; } = null!;

    public string Text { get; set; } = null!;

    public int Position { get; set; }

    public ICollection<PollVote> Votes { get; set; } =
        new List<PollVote>();
}
