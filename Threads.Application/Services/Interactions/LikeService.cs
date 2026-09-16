using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Likes;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Interactions;

public class LikeService : ILikeService
{
    private readonly ILikeRepository _likeRepository;
    private readonly IPostRepository _postRepository;
    private readonly IPostService _postService;
    private readonly ICommentService _commentService;

    public LikeService(
        ILikeRepository likeRepository,
        IPostRepository postRepository,
        IPostService postService,
        ICommentService commentService)
    {
        _likeRepository = likeRepository;
        _postRepository = postRepository;
        _postService = postService;
        _commentService = commentService;
    }

    public async Task<UserLikesPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postService.GetLikedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);
        var comments = await _commentService.GetLikedByUserIdAsync(
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

    public async Task<bool> AddLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);

        if (post is null)
        {
            return false;
        }

        var existingLike = await _likeRepository.GetByUserAndPostAsync(
            userId,
            postId,
            cancellationToken);

        if (existingLike is not null)
        {
            return false;
        }

        var like = new PostLike
        {
            UserId = userId,
            PostId = postId
        };

        try
        {
            await _likeRepository.AddAsync(like, cancellationToken);
        }
        catch (Exception exception) when (IsDuplicateWriteException(exception))
        {
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var existingLike = await _likeRepository.GetByUserAndPostAsync(
            userId,
            postId,
            cancellationToken);

        if (existingLike is null)
        {
            return false;
        }

        await _likeRepository.DeleteAsync(existingLike, cancellationToken);

        return true;
    }

    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }

    private sealed record LikedItem(
        DateTimeOffset ActionAt,
        Guid Id,
        PostResponse? Post,
        CommentResponse? Comment);
}
