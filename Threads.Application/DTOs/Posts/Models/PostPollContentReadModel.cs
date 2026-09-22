namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostPollContentReadModel
{
    public Guid Id { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public IReadOnlyCollection<PostPollOptionContentReadModel> Options { get; init; } = [];
}
