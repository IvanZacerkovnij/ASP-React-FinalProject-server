namespace Threads.Domain.Models.Versions;

public sealed class PollVersionSnapshot
{
    public Guid Id { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public List<PollOptionVersionSnapshot> Options { get; init; } = [];
}