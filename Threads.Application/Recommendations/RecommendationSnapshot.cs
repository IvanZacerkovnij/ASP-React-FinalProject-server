namespace Threads.Application.Recommendations;

public sealed record RecommendationSnapshot(
    Guid UserId,
    string Kind,
    DateTimeOffset ExpiresAt,
    Guid[] RemainingIds);
