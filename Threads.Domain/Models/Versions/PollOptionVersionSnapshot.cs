namespace Threads.Domain.Models.Versions;

public sealed class PollOptionVersionSnapshot
{
    public Guid Id { get; init; }

    public required string Text { get; init; }

    public int Position { get; init; }
}