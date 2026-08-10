namespace Threads.Application.DTOs.Posts;

public class CreatePostRequest
{
    public string? Content { get; init; }

    public IReadOnlyCollection<Guid> MediaIds { get; init; } = [];

    public CreatePostPollRequest? Poll { get; init; }

    public PostLocationRequest? Location { get; init; }

    public PostEmbedRequest? Embed { get; init; }
}
