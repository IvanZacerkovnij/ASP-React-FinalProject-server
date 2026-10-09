using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Recommendations;
using Threads.Application.Recommendations;

namespace Threads.Infrastructure.Data.Repositories.Recommendations;

public sealed class RecommendationRepository(ThreadsDbContext dbContext, IOptions<RecommendationOptions> options)
    : IRecommendationRepository
{
    private readonly RecommendationOptions _options = options.Value;

    private const string GraphSql = """
        WITH active_users AS MATERIALIZED (
            SELECT "Id", "CreatedAt" FROM "Users" WHERE "DeletedAt" IS NULL AND "IsActive"
        ), visible_posts AS (
            SELECT p."Id", p."AuthorId", p."CreatedAt" FROM "Posts" p
            JOIN active_users u ON u."Id" = p."AuthorId"
            WHERE p."DeletedAt" IS NULL AND p."CreatedAt" <= @now
        ), visible_comments AS (
            SELECT c."Id", c."PostId" FROM "Comments" c
            JOIN visible_posts p ON p."Id" = c."PostId"
            JOIN active_users u ON u."Id" = c."AuthorId"
            WHERE c."DeletedAt" IS NULL
        ), candidates AS MATERIALIZED (
            SELECT * FROM visible_posts WHERE (@viewer IS NULL OR "AuthorId" <> @viewer) AND "CreatedAt" >= @post_since
            ORDER BY "CreatedAt" DESC, "Id" DESC LIMIT @candidate_limit
        ), history_refs AS (
            (SELECT i."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "PostLikes" i  WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."PostId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT i."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "PostReposts" i  WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."PostId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT i."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "PostBookmarks" i  WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."PostId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT i."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "PostViews" i  WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."PostId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT i."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "Comments" i JOIN visible_comments c ON c."Id" = i."Id" WHERE i."AuthorId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."PostId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT c."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "CommentLikes" i JOIN visible_comments c ON c."Id" = i."CommentId" WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."CommentId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT c."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "CommentReposts" i JOIN visible_comments c ON c."Id" = i."CommentId" WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."CommentId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT c."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "CommentBookmarks" i JOIN visible_comments c ON c."Id" = i."CommentId" WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."CommentId" DESC LIMIT @history_limit)
            UNION ALL
            (SELECT c."PostId" AS post_id, i."CreatedAt" AS acted_at FROM "CommentViews" i JOIN visible_comments c ON c."Id" = i."CommentId" WHERE i."UserId" = @viewer AND i."CreatedAt" BETWEEN @history_since AND @now ORDER BY i."CreatedAt" DESC, i."CommentId" DESC LIMIT @history_limit)
        ), history_posts AS (
            SELECT post_id FROM history_refs GROUP BY post_id
            ORDER BY MAX(acted_at) DESC, post_id DESC LIMIT @history_limit
        ), scope_posts AS MATERIALIZED (
            SELECT * FROM visible_posts WHERE "Id" IN (SELECT "Id" FROM candidates)
                OR "Id" IN (SELECT post_id FROM history_posts)
        ), raw_edges AS (
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @like::double precision AS weight, true AS explicit FROM "PostLikes" i  JOIN scope_posts p ON p."Id" = i."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @repost::double precision AS weight, true AS explicit FROM "PostReposts" i  JOIN scope_posts p ON p."Id" = i."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @bookmark::double precision AS weight, true AS explicit FROM "PostBookmarks" i  JOIN scope_posts p ON p."Id" = i."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @view::double precision AS weight, false AS explicit FROM "PostViews" i  JOIN scope_posts p ON p."Id" = i."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."AuthorId" AS user_id, p."Id" AS post_id, @comment::double precision AS weight, true AS explicit FROM "Comments" i JOIN visible_comments c ON c."Id" = i."Id" JOIN scope_posts p ON p."Id" = i."PostId" JOIN active_users u ON u."Id" = i."AuthorId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @like::double precision AS weight, true AS explicit FROM "CommentLikes" i JOIN visible_comments c ON c."Id" = i."CommentId" JOIN scope_posts p ON p."Id" = c."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @repost::double precision AS weight, true AS explicit FROM "CommentReposts" i JOIN visible_comments c ON c."Id" = i."CommentId" JOIN scope_posts p ON p."Id" = c."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @bookmark::double precision AS weight, true AS explicit FROM "CommentBookmarks" i JOIN visible_comments c ON c."Id" = i."CommentId" JOIN scope_posts p ON p."Id" = c."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
            UNION ALL
            SELECT DISTINCT i."UserId" AS user_id, p."Id" AS post_id, @view::double precision AS weight, false AS explicit FROM "CommentViews" i JOIN visible_comments c ON c."Id" = i."CommentId" JOIN scope_posts p ON p."Id" = c."PostId" JOIN active_users u ON u."Id" = i."UserId" WHERE i."CreatedAt" BETWEEN @history_since AND @now
        ), edges AS MATERIALIZED (
            SELECT user_id, post_id, LEAST(@signal_cap, SUM(weight)) AS weight,
                BOOL_OR(explicit) AS explicit
            FROM raw_edges GROUP BY user_id, post_id
        ), affinity AS (
            SELECT p."AuthorId" AS author_id, LEAST(@author_cap, SUM(e.weight)) AS score
            FROM edges e JOIN scope_posts p ON p."Id" = e.post_id
            WHERE e.user_id = @viewer GROUP BY p."AuthorId"
        ), peers AS MATERIALIZED (
            SELECT other.user_id, LEAST(@author_cap, SUM(LEAST(mine.weight, other.weight))) AS score
            FROM edges mine JOIN edges other ON mine.post_id = other.post_id
            WHERE mine.user_id = @viewer AND other.user_id <> @viewer
                AND mine.explicit AND other.explicit
            GROUP BY other.user_id ORDER BY score DESC, other.user_id DESC LIMIT @peer_limit
        ), collaborative AS (
            SELECT e.post_id, LEAST(@collaborative_cap, SUM(e.weight * peers.score / @author_cap)) AS score
            FROM edges e JOIN peers ON peers.user_id = e.user_id
            GROUP BY e.post_id
        ), popularity AS (
            SELECT post_id, LEAST(@popularity_cap, SUM(weight)) AS score FROM edges GROUP BY post_id
        ), visible_follows AS MATERIALIZED (
            SELECT f."FollowerId" AS follower_id, f."FollowingId" AS following_id FROM "Follows" f
            JOIN active_users follower ON follower."Id" = f."FollowerId"
            JOIN active_users following ON following."Id" = f."FollowingId"
            WHERE f."CreatedAt" <= @now
        ), following AS (
            SELECT following_id FROM visible_follows WHERE follower_id = @viewer
        )
        """;

    private const string PostRankingSql = """
            , scored AS (
                SELECT p.*,
                    @author_weight * COALESCE(a.score, 0)
                    + @follow_weight * CASE WHEN f.following_id IS NULL THEN 0 ELSE 1 END
                    + @collaborative_weight * COALESCE(c.score, 0)
                    + @popularity_weight * COALESCE(pop.score, 0)
                    + @freshness_weight / (1 + EXTRACT(EPOCH FROM (@now - p."CreatedAt")) / 3600 / @half_life) AS score
                FROM candidates p
                LEFT JOIN affinity a ON a.author_id = p."AuthorId"
                LEFT JOIN following f ON f.following_id = p."AuthorId"
                LEFT JOIN collaborative c ON c.post_id = p."Id"
                LEFT JOIN popularity pop ON pop.post_id = p."Id"
            ), diversified AS (
                SELECT *, ROW_NUMBER() OVER (
                    PARTITION BY "AuthorId" ORDER BY score DESC, "CreatedAt" DESC, "Id" DESC
                ) AS author_rank FROM scored
            )
            """;

    public async Task<IReadOnlyCollection<Guid>> RankPostsAsync(
        Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        const string selectionSql = """
            SELECT "Id" FROM diversified
            ORDER BY author_rank, score DESC, "CreatedAt" DESC, "Id" DESC LIMIT @result_limit
            """;
        return await RankAsync(GraphSql + PostRankingSql + selectionSql, userId, asOf, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetFeedIdsAsync(
        Guid? userId, int count, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        const string selectionSql = """
            , recommendation_pool AS MATERIALIZED (
                SELECT * FROM diversified
                ORDER BY author_rank, score DESC, "CreatedAt" DESC, "Id" DESC LIMIT @result_limit
            ), sampled AS MATERIALIZED (
                SELECT *, -LN(GREATEST(RANDOM(), 0.000000000001)) / (1 + score) AS priority
                FROM recommendation_pool
            ), randomized AS (
                SELECT *, ROW_NUMBER() OVER (
                    PARTITION BY "AuthorId" ORDER BY priority, "Id" DESC
                ) AS random_author_rank FROM sampled
            )
            SELECT "Id" FROM randomized
            ORDER BY random_author_rank, priority, "Id" DESC LIMIT @feed_count
            """;
        return await RankAsync(GraphSql + PostRankingSql + selectionSql, userId, asOf, cancellationToken, count);
    }

    public async Task<IReadOnlyCollection<Guid>> RankUsersAsync(
        Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        const string rankingSql = """
            , second_degree AS (
                SELECT f.following_id AS user_id, LEAST(@connection_cap, COUNT(*)) AS score
                FROM visible_follows f JOIN following mine ON mine.following_id = f.follower_id
                GROUP BY f.following_id ORDER BY score DESC, f.following_id DESC LIMIT @candidate_limit
            ), shared_follows AS (
                SELECT f.follower_id AS user_id, LEAST(@connection_cap, COUNT(*)) AS score
                FROM visible_follows f JOIN following mine ON mine.following_id = f.following_id
                GROUP BY f.follower_id ORDER BY score DESC, f.follower_id DESC LIMIT @candidate_limit
            ), follower_counts AS (
                SELECT following_id AS user_id, LEAST(@popularity_cap, COUNT(*)) AS score
                FROM visible_follows GROUP BY following_id
            ), fallback AS (
                SELECT u."Id" AS user_id FROM active_users u
                LEFT JOIN follower_counts f ON f.user_id = u."Id"
                WHERE u."Id" <> @viewer AND NOT EXISTS (
                    SELECT 1 FROM following WHERE following_id = u."Id")
                ORDER BY COALESCE(f.score, 0) DESC, u."CreatedAt" DESC, u."Id" DESC LIMIT @candidate_limit
            ), user_candidates AS (
                SELECT user_id FROM second_degree UNION SELECT user_id FROM shared_follows
                UNION SELECT author_id FROM affinity UNION SELECT user_id FROM fallback
            ), scored_users AS (
                SELECT u."Id", u."CreatedAt",
                    @second_degree_weight * COALESCE(d.score, 0)
                    + @shared_follow_weight * COALESCE(s.score, 0)
                    + @author_weight * COALESCE(a.score, 0)
                    + @popularity_weight * COALESCE(f.score, 0) AS score
                FROM user_candidates candidate JOIN active_users u ON u."Id" = candidate.user_id
                LEFT JOIN second_degree d ON d.user_id = u."Id"
                LEFT JOIN shared_follows s ON s.user_id = u."Id"
                LEFT JOIN affinity a ON a.author_id = u."Id"
                LEFT JOIN follower_counts f ON f.user_id = u."Id"
                WHERE u."Id" <> @viewer AND NOT EXISTS (
                    SELECT 1 FROM following WHERE following_id = u."Id")
            )
            SELECT "Id" FROM scored_users ORDER BY score DESC, "CreatedAt" DESC, "Id" DESC LIMIT @result_limit
            """;

        return await RankAsync(GraphSql + rankingSql, userId, asOf, cancellationToken);
    }

    private async Task<IReadOnlyCollection<Guid>> RankAsync(
        string sql, Guid? userId, DateTimeOffset asOf, CancellationToken cancellationToken, int? count = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["viewer"] = userId.HasValue ? userId.Value : DBNull.Value, ["now"] = asOf,
            ["feed_count"] = count ?? _options.ResultLimit,
            ["history_since"] = asOf.AddDays(-_options.HistoryWindowDays),
            ["post_since"] = asOf.AddDays(-_options.PostWindowDays),
            ["candidate_limit"] = _options.CandidateLimit, ["history_limit"] = _options.HistoryLimit,
            ["peer_limit"] = _options.PeerLimit, ["result_limit"] = _options.ResultLimit,
            ["like"] = _options.LikeWeight, ["comment"] = _options.CommentWeight,
            ["repost"] = _options.RepostWeight, ["bookmark"] = _options.BookmarkWeight,
            ["view"] = _options.ViewWeight, ["signal_cap"] = _options.SignalCap,
            ["author_cap"] = _options.AuthorCap, ["collaborative_cap"] = _options.CollaborativeCap,
            ["popularity_cap"] = _options.PopularityCap, ["connection_cap"] = _options.ConnectionCap,
            ["author_weight"] = _options.AuthorWeight, ["follow_weight"] = _options.FollowWeight,
            ["collaborative_weight"] = _options.CollaborativeWeight,
            ["popularity_weight"] = _options.PopularityWeight, ["freshness_weight"] = _options.FreshnessWeight,
            ["half_life"] = _options.FreshnessHalfLifeHours,
            ["second_degree_weight"] = _options.SecondDegreeWeight, ["shared_follow_weight"] = _options.SharedFollowWeight
        };
        var rows = await dbContext.Database.SqlQueryRaw<RecommendationId>(
                sql, parameters.Select(parameter => (object)(parameter.Key == "viewer"
                    ? new NpgsqlParameter(parameter.Key, NpgsqlDbType.Uuid) { Value = parameter.Value }
                    : new NpgsqlParameter(parameter.Key, parameter.Value))).ToArray())
            .ToListAsync(cancellationToken);
        return rows.Select(row => row.Id).ToArray();
    }

    public async Task<IReadOnlyCollection<Guid>> GetAvailableIdsAsync(
        Guid userId, string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return kind == "posts"
            ? await dbContext.Posts.AsNoTracking()
                .Where(post => ids.Contains(post.Id) && post.AuthorId != userId)
                .Select(post => post.Id).ToListAsync(cancellationToken)
            : await dbContext.Users.AsNoTracking()
                .Where(user => ids.Contains(user.Id) && user.Id != userId &&
                    !dbContext.Follows.Any(follow => follow.FollowerId == userId && follow.FollowingId == user.Id))
                .Select(user => user.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserSummaryReadModel>> GetUsersAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users.AsNoTracking().Where(user => ids.Contains(user.Id))
            .Select(user => new UserSummaryReadModel
            {
                Id = user.Id, Username = user.Username, DisplayName = user.DisplayName, Bio = user.Bio,
                AvatarObjectKey = user.AvatarObjectKey, IsVerified = user.IsVerified,
                LocationPlaceId = user.LocationPlaceId, LocationName = user.Location,
                LocationCountry = user.LocationCountry, LocationLatitude = user.LocationLatitude,
                LocationLongitude = user.LocationLongitude
            }).ToListAsync(cancellationToken);
    }

    public sealed class RecommendationId
    {
        public Guid Id { get; init; }
    }
}
