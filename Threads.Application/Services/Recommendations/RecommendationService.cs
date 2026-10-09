using System.Text.Json;
using Microsoft.Extensions.Options;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Quotes;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Recommendations;
using Threads.Application.Recommendations;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Common;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Users;
using Threads.Domain.Enums;

namespace Threads.Application.Services.Recommendations;

public sealed class RecommendationService(
    IRecommendationRepository repository,
    IRecommendationCursorProtector cursorProtector,
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    PostResponseFactory postFactory,
    CommentResponseFactory commentFactory,
    UserResponseFactory userFactory,
    IOptions<RecommendationOptions> options,
    TimeProvider timeProvider) : IRecommendationService
{
    public async Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(
        Guid? userId, int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 50)
        {
            throw new RequestValidationException("Feed count must be between 1 and 50.");
        }

        var ids = await repository.GetFeedIdsAsync(userId, count, timeProvider.GetUtcNow(), cancellationToken);
        return await CreatePostResponsesAsync(ids.Distinct().Take(count).ToArray(), userId, cancellationToken);
    }

    public async Task<CursorPageResponse<PostResponse>> GetPostsAsync(
        Guid userId, CursorPageRequest request, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(userId, "posts", request, cancellationToken);
        var pageIds = snapshot.RemainingIds.Take(request.Limit).ToArray();
        var responses = await CreatePostResponsesAsync(pageIds, userId, cancellationToken);
        return CreatePage(snapshot, request.Limit, responses);
    }

    private async Task<IReadOnlyCollection<PostResponse>> CreatePostResponsesAsync(
        Guid[] pageIds, Guid? userId, CancellationToken cancellationToken)
    {
        var posts = await postRepository.GetSummariesByIdsAsync(pageIds, userId, cancellationToken);
        var responses = posts.Select(postFactory.Create).ToDictionary(post => post.Id);
        var quotes = posts.Where(post => post.Quote is not null).Select(post => post.Quote!).ToArray();
        var quotedPosts = await postRepository.GetSummariesByIdsAsync(
            quotes.Where(quote => quote.TargetType == ContentTargetType.Post).Select(quote => quote.TargetId).Distinct().ToArray(),
            userId, cancellationToken);
        var quotedComments = await commentRepository.GetSummariesByIdsAsync(
            quotes.Where(quote => quote.TargetType == ContentTargetType.Comment).Select(quote => quote.TargetId).Distinct().ToArray(),
            userId, cancellationToken);
        var postTargets = quotedPosts.ToDictionary(post => post.Id);
        var commentTargets = quotedComments.ToDictionary(comment => comment.Id);

        foreach (var post in posts)
        {
            if (post.Quote is not { } quote)
            {
                continue;
            }

            object? target = null;
            Guid? versionId = null;
            if (quote.TargetType == ContentTargetType.Post && postTargets.TryGetValue(quote.TargetId, out var quotedPost))
            {
                target = postFactory.Create(quotedPost);
                versionId = quotedPost.VersionId;
            }
            else if (quote.TargetType == ContentTargetType.Comment && commentTargets.TryGetValue(quote.TargetId, out var quotedComment))
            {
                target = commentFactory.Create(quotedComment);
                versionId = quotedComment.VersionId;
            }

            responses[post.Id].Quote = new QuoteResponse
            {
                TargetType = quote.TargetType, TargetId = quote.TargetId,
                TargetVersionId = quote.TargetVersionId, Target = target,
                HasNewVersion = versionId.HasValue && versionId.Value != quote.TargetVersionId
            };
        }

        return pageIds.Where(responses.ContainsKey).Select(id => responses[id]).ToArray();
    }

    public async Task<CursorPageResponse<UserShortResponse>> GetUsersAsync(
        Guid userId, CursorPageRequest request, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(userId, "users", request, cancellationToken);
        var pageIds = snapshot.RemainingIds.Take(request.Limit).ToArray();
        var users = await repository.GetUsersAsync(pageIds, cancellationToken);
        var responses = users.Select(userFactory.CreateShort).ToDictionary(user => user.Id);
        return CreatePage(snapshot, request.Limit,
            pageIds.Where(responses.ContainsKey).Select(id => responses[id]).ToArray());
    }

    private async Task<RecommendationSnapshot> GetSnapshotAsync(
        Guid userId, string kind, CursorPageRequest request, CancellationToken cancellationToken)
    {
        RequestValidator.Validate(request);
        var now = timeProvider.GetUtcNow();
        RecommendationSnapshot snapshot;
        if (string.IsNullOrWhiteSpace(request.Cursor))
        {
            var ids = kind == "posts"
                ? await repository.RankPostsAsync(userId, now, cancellationToken)
                : await repository.RankUsersAsync(userId, now, cancellationToken);
            snapshot = new RecommendationSnapshot(userId, kind,
                now.AddMinutes(options.Value.CursorLifetimeMinutes), ids.Distinct().Take(options.Value.ResultLimit).ToArray());
        }
        else
        {
            if (request.Cursor.Length > 8_000)
            {
                throw new RequestValidationException("Recommendation cursor is invalid.");
            }

            try
            {
                snapshot = JsonSerializer.Deserialize<RecommendationSnapshot>(cursorProtector.Unprotect(request.Cursor))
                    ?? throw new JsonException();
            }
            catch (JsonException)
            {
                throw new RequestValidationException("Recommendation cursor is invalid.");
            }

            if (snapshot.UserId != userId || snapshot.Kind != kind || snapshot.ExpiresAt <= now ||
                snapshot.RemainingIds is null || snapshot.RemainingIds.Length > RecommendationOptions.MaximumResultLimit)
            {
                throw new RequestValidationException("Recommendation cursor is invalid or expired.");
            }
        }

        var available = (await repository.GetAvailableIdsAsync(userId, kind, snapshot.RemainingIds, cancellationToken)).ToHashSet();
        return snapshot with { RemainingIds = snapshot.RemainingIds.Where(available.Contains).Distinct().ToArray() };
    }

    private CursorPageResponse<T> CreatePage<T>(RecommendationSnapshot snapshot, int limit, IReadOnlyCollection<T> items)
    {
        var remaining = snapshot.RemainingIds.Skip(limit).ToArray();
        return new CursorPageResponse<T>
        {
            Items = items, HasMore = remaining.Length > 0,
            NextCursor = remaining.Length > 0
                ? cursorProtector.Protect(JsonSerializer.Serialize(snapshot with { RemainingIds = remaining }))
                : null
        };
    }
}
