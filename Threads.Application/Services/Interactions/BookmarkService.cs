using Threads.Application.DTOs.Bookmarks;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Common;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Interactions;

public class BookmarkService : IBookmarkService
{
    private readonly IBookmarkRepository _bookmarkRepository;
    private readonly IPostRepository _postRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly PostQueryService _postQueryService;
    private readonly CommentQueryService _commentQueryService;

    public BookmarkService(
        IBookmarkRepository bookmarkRepository,
        IPostRepository postRepository,
        ICommentRepository commentRepository,
        PostQueryService postQueryService,
        CommentQueryService commentQueryService)
    {
        _bookmarkRepository = bookmarkRepository;
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _postQueryService = postQueryService;
        _commentQueryService = commentQueryService;
    }

    public async Task<UserBookmarksPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postQueryService.GetBookmarkedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);
        var comments = await _commentQueryService.GetBookmarkedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);

        var bookmarkedItems = posts
            .Where(post => post.ActionAt.HasValue)
            .Select(post => new BookmarkedItem(
                post.ActionAt.Value,
                post.Id,
                post,
                Comment: null))
            .Concat(comments
                .Where(comment => comment.ActionAt.HasValue)
                .Select(comment => new BookmarkedItem(
                    comment.ActionAt.Value,
                    comment.Id,
                    Post: null,
                    Comment: comment)))
            .OrderByDescending(item => item.ActionAt)
            .ThenByDescending(item => item.Id)
            .Take(pagination.Limit + 1)
            .ToList();

        var hasMore = bookmarkedItems.Count > pagination.Limit;
        var pageItems = bookmarkedItems.Take(pagination.Limit).ToList();

        return new UserBookmarksPageResponse
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
    
    public async Task<bool> AddPostBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var postExists = await _postRepository.ExistsAsync(postId, cancellationToken);
        
        if (!postExists)
        {
            return false;
        }

        var bookmark = new PostBookmark
        {
            UserId = userId,
            PostId = postId
        };

        return await _bookmarkRepository.TryAddAsync(bookmark, cancellationToken);
    }
    public async Task<bool> AddCommentBookmarkAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        var commentExists = await _commentRepository.ExistsAsync(commentId, cancellationToken);
        
        if (!commentExists)
        {
            return false;
        }

        var bookmark = new CommentBookmark
        {
            UserId = userId,
            CommentId = commentId
        };

        return await _bookmarkRepository.TryAddAsync(bookmark, cancellationToken);
    }

    public Task<bool> RemovePostBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        return _bookmarkRepository.TryDeletePostAsync(
            userId,
            postId,
            cancellationToken);
    }
    
    public Task<bool> RemoveCommentBookmarkAsync(Guid userId, Guid commentId, CancellationToken cancellationToken = default)
    {
        return _bookmarkRepository.TryDeleteCommentAsync(
            userId,
            commentId,
            cancellationToken);
    }

    private sealed record BookmarkedItem(
        DateTimeOffset ActionAt,
        Guid Id,
        PostResponse? Post,
        CommentResponse? Comment);
}
