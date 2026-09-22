namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostPollSummaryReadModel
{
    public Guid Id { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public int TotalVotes { get; init; }

    public Guid? SelectedOptionId { get; init; }

    public IReadOnlyCollection<PostPollOptionSummaryReadModel> Options { get; init; } = [];
}
