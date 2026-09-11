namespace Threads.Application.DTOs.Posts.Responses;

public class PostViewResponse
{
    public Guid PostId { get; init; }

    public int ViewsCount { get; init; }
}
