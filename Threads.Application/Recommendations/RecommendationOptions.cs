using System.ComponentModel.DataAnnotations;

namespace Threads.Application.Recommendations;

public sealed class RecommendationOptions
{
    public const int MaximumResultLimit = 200;

    [Range(50, 5000)] public int CandidateLimit { get; init; } = 1000;
    [Range(50, MaximumResultLimit)] public int ResultLimit { get; init; } = MaximumResultLimit;
    [Range(1, 1000)] public int HistoryLimit { get; init; } = 500;
    [Range(1, 200)] public int PeerLimit { get; init; } = 100;
    [Range(1, 365)] public int PostWindowDays { get; init; } = 30;
    [Range(1, 365)] public int HistoryWindowDays { get; init; } = 90;
    [Range(1, 60)] public int CursorLifetimeMinutes { get; init; } = 30;
    [Range(0, 100)] public double LikeWeight { get; init; } = 3;
    [Range(0, 100)] public double CommentWeight { get; init; } = 4;
    [Range(0, 100)] public double RepostWeight { get; init; } = 5;
    [Range(0, 100)] public double BookmarkWeight { get; init; } = 5;
    [Range(0, 1)] public double ViewWeight { get; init; } = 0.25;
    [Range(0, 100)] public double FollowWeight { get; init; } = 6;
    [Range(0, 100)] public double AuthorWeight { get; init; } = 1;
    [Range(0, 100)] public double CollaborativeWeight { get; init; } = 2;
    [Range(0, 100)] public double FreshnessWeight { get; init; } = 4;
    [Range(0, 100)] public double PopularityWeight { get; init; } = 0.2;
    [Range(0, 100)] public double SecondDegreeWeight { get; init; } = 4;
    [Range(0, 100)] public double SharedFollowWeight { get; init; } = 2;
    [Range(1, 1000)] public double SignalCap { get; init; } = 10;
    [Range(1, 1000)] public double AuthorCap { get; init; } = 20;
    [Range(1, 1000)] public double CollaborativeCap { get; init; } = 30;
    [Range(1, 1000)] public double PopularityCap { get; init; } = 100;
    [Range(1, 1000)] public double ConnectionCap { get; init; } = 20;
    [Range(1, 720)] public double FreshnessHalfLifeHours { get; init; } = 48;

    public bool HasValidWeights() => ViewWeight < LikeWeight && ViewWeight < CommentWeight &&
        ViewWeight < RepostWeight && ViewWeight < BookmarkWeight && ResultLimit <= CandidateLimit;
}
