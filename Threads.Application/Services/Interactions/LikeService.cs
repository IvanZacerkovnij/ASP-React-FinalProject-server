using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Likes;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Common;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Interactions;

public class LikeService : ILikeService
{
    private readonly ILikeRepository _likeRepository;
    private readonly IPostRepository _postRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly PostQueryService _postQueryService;
    private readonly CommentQueryService _commentQueryService;

    public LikeService(
        ILikeRepository likeRepository,
        IPostRepository postRepository, 
        ICommentRepository commentRepository,
        PostQueryService postQueryService,
        CommentQueryService commentQueryService)
    {
        _likeRepository = likeRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _postQueryService = postQueryService;
        _commentQueryService = commentQueryService;
    }

    public async Task<UserLikesPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postQueryService.GetLikedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);
        var comments = await _commentQueryService.GetLikedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);

        var likedItems = posts
            .Where(post => post.ActionAt.HasValue)
            .Select(post => new LikedItem(
                post.ActionAt.Value,
                post.Id,
                post,
                Comment: null))
            .Concat(comments
                .Where(comment => comment.ActionAt.HasValue)
                .Select(comment => new LikedItem(
                    comment.ActionAt.Value,
                    comment.Id,
                    Post: null,
                    Comment: comment)))
            .OrderByDescending(item => item.ActionAt)
            .ThenByDescending(item => item.Id)
            .Take(pagination.Limit + 1)
            .ToList();

        var hasMore = likedItems.Count > pagination.Limit;
        var pageItems = likedItems.Take(pagination.Limit).ToList();

        return new UserLikesPageResponse
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

    public async Task<bool> AddCommentLikeAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        var commentExists = await _commentRepository.ExistsAsync(commentId, cancellationToken);

        if (!commentExists)
        {
            return false;
        }

        var like = new CommentLike()
        {
            UserId = userId,
            CommentId = commentId
        };
        
        return await _likeRepository.TryAddAsync(like, cancellationToken);
    }
    
    public async Task<bool> AddPostLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var postExists = await _postRepository.ExistsAsync(postId, cancellationToken);

        if (!postExists)
        {
            return false;
        }

        var like = new PostLike
        {
            UserId = userId,
            PostId = postId
        };
        
        return await _likeRepository.TryAddAsync(like, cancellationToken);
    }

    public Task<bool> RemovePostLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        return _likeRepository.TryDeletePostAsync(
            userId,
            postId,
            cancellationToken);
    }

    public Task<bool> RemoveCommentLikeAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        return _likeRepository.TryDeleteCommentAsync(
            userId,
            commentId,
            cancellationToken);
    }

    private sealed record LikedItem(
        DateTimeOffset ActionAt,
        Guid Id,
        PostResponse? Post,
        CommentResponse? Comment);
}
