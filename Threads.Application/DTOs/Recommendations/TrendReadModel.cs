namespace Threads.Application.DTOs.Recommendations;

public sealed class TrendReadModel
{
    public required string Topic { get; init; }
    public int PostsCount { get; init; }
}
