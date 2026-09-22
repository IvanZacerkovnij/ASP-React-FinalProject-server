namespace Threads.Application.DTOs.Posts.Models;

public sealed class PostPollEngagementReadModel
{
    public int TotalVotes { get; init; }

    public Guid? SelectedOptionId { get; init; }

    public IReadOnlyCollection<PostPollOptionEngagementReadModel> Options { get; init; } = [];
}
