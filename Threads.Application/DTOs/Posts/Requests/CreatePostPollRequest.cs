namespace Threads.Application.DTOs.Posts.Requests;

public class CreatePostPollRequest
{
    public IReadOnlyCollection<string> Options { get; init; } = [];

    public DateTime? EndsAt { get; init; }
}
