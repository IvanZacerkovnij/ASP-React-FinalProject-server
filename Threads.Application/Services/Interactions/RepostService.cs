using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Reposts;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Interactions;

public class RepostService : IRepostService
{
    private readonly IRepostRepository _repostRepository;
    private readonly IPostRepository _postRepository;
    private readonly IPostService _postService;
    private readonly ICommentService _commentService;

    public RepostService(
        IRepostRepository repostRepository,
        IPostRepository postRepository,
        IPostService postService,
        ICommentService commentService)
    {
        _repostRepository = repostRepository;
        _postRepository = postRepository;
        _postService = postService;
        _commentService = commentService;
    }

    public async Task<UserRepostsPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postService.GetRepostedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);
        var comments = await _commentService.GetRepostedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);

        var repostedItems = posts
            .Where(post => post.ActionAt.HasValue)
            .Select(post => new RepostedItem(
                post.ActionAt.Value,
                post.Id,
                post,
                Comment: null))
            .Concat(comments
                .Where(comment => comment.ActionAt.HasValue)
                .Select(comment => new RepostedItem(
                    comment.ActionAt.Value,
                    comment.Id,
                    Post: null,
                    Comment: comment)))
            .OrderByDescending(item => item.ActionAt)
            .ThenByDescending(item => item.Id)
            .Take(pagination.Limit + 1)
            .ToList();

        var hasMore = repostedItems.Count > pagination.Limit;
        var pageItems = repostedItems.Take(pagination.Limit).ToList();

        return new UserRepostsPageResponse
        {
            Posts = pageItems
                .Where(item => item.Post is not null)
                .Select(item => item.Post!)
                .ToList(),
            Comments = pageItems
                .Where(item => item.Comment is not null)
                .Select(item => item.Comment!)
                .ToList(),
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.Encode(pageItems[^1].ActionAt, pageItems[^1].Id)
                : null
        };
    }

    public async Task<bool> AddRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);

        if (post is null)
        {
            return false;
        }

        var existingRepost = await _repostRepository.GetByUserAndPostAsync(userId, postId, cancellationToken);

        if (existingRepost is not null)
        {
            return false;
        }

        var repost = new PostRepost
        {
            UserId = userId,
            PostId = postId
        };

        try
        {
            await _repostRepository.AddAsync(repost, cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateWriteException(exception))
        {
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var repost = await _repostRepository.GetByUserAndPostAsync(userId, postId, cancellationToken);

        if (repost is null)
        {
            return false;
        }

        await _repostRepository.DeleteAsync(repost, cancellationToken);

        return true;
    }

    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }

    private sealed record RepostedItem(
        DateTimeOffset ActionAt,
        Guid Id,
        PostResponse? Post,
        CommentResponse? Comment);
}
