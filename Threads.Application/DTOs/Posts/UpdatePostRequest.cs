namespace Threads.Application.DTOs.Posts;

public class UpdatePostRequest
{
    public string? Content { get; init; }

    public IReadOnlyCollection<Guid>? MediaIds { get; init; }

    public CreatePostPollRequest? Poll { get; init; }

    public bool RemoveLocation { get; init; }

    public PostLocationRequest? Location { get; init; }

    public bool RemoveEmbed { get; init; }

    public PostEmbedRequest? Embed { get; init; }
}
