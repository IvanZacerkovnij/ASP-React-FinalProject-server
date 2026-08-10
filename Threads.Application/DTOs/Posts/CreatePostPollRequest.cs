namespace Threads.Application.DTOs.Posts;

public class CreatePostPollRequest
{
    public IReadOnlyCollection<string> Options { get; init; } = [];

    public DateTime? EndsAt { get; init; }
}
