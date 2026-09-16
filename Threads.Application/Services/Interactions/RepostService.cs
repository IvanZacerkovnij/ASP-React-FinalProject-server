using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Reposts;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Services.Common;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Interactions;

public class RepostService : IRepostService
{
    private readonly IRepostRepository _repostRepository;
    private readonly IPostRepository _postRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly PostQueryService _postQueryService;
    private readonly CommentQueryService _commentQueryService;

    public RepostService(
        IRepostRepository repostRepository,
        IPostRepository postRepository,
        ICommentRepository commentRepository,
        PostQueryService postQueryService,
        CommentQueryService commentQueryService)
    {
        _repostRepository = repostRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _postQueryService = postQueryService;
        _commentQueryService = commentQueryService;
    }

    public async Task<UserRepostsPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postQueryService.GetRepostedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);
        var comments = await _commentQueryService.GetRepostedByUserIdAsync(
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

    public async Task<bool> AddPostRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var postExists = await _postRepository.ExistsAsync(postId, cancellationToken);

        if (!postExists)
        {
            return false;
        }

        var repost = new PostRepost
        {
            UserId = userId,
            PostId = postId
        };

        return await _repostRepository.TryAddAsync(repost, cancellationToken);
    }
    
    public async Task<bool> AddCommentRepostAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        var commentExists = await _commentRepository.ExistsAsync(commentId, cancellationToken);

        if (!commentExists)
        {
            return false;
        }

        var repost = new CommentRepost
        {
            UserId = userId,
            CommentId = commentId
        };

        return await _repostRepository.TryAddAsync(repost, cancellationToken);
    }

    public Task<bool> RemovePostRepostAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        return _repostRepository.TryDeletePostAsync(userId, postId, cancellationToken);
    }
    
    public Task<bool> RemoveCommentRepostAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        return _repostRepository.TryDeleteCommentAsync(userId, commentId, cancellationToken);
    }

    private sealed record RepostedItem(
        DateTimeOffset ActionAt,
        Guid Id,
        PostResponse? Post,
        CommentResponse? Comment);
}
