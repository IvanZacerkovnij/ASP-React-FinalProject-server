namespace Threads.Application.DTOs.Posts;

public sealed class PostPollReadModel
{
    public Guid Id { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public IReadOnlyCollection<PostPollOptionReadModel> Options { get; init; } = [];
}
