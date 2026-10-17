namespace Threads.Application.DTOs.Recommendations;

public sealed class TrendRecommendationsResponse
{
    public required IReadOnlyCollection<TrendRecommendation> Items { get; init; }
}
