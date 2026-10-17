using System.Text.Json.Serialization;

namespace Threads.Application.DTOs.Recommendations;

public sealed class TrendRecommendation
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Context { get; init; }

    public required string Query { get; init; }

    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int PostsCount { get; init; }
}
