namespace Threads.Application.DTOs.Posts;

public sealed class PostPollStateReadModel
{
    public int TotalVotes { get; init; }

    public Guid? SelectedOptionId { get; init; }

    public IReadOnlyCollection<PostPollOptionStateReadModel> Options { get; init; } = [];
}
