using Threads.Application.DTOs.Bookmarks;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Interactions;

public class BookmarkService : IBookmarkService
{
    private readonly IBookmarkRepository _bookmarkRepository;
    private readonly IPostRepository _postRepository;
    private readonly IPostService _postService;
    private readonly ICommentService _commentService;

    public BookmarkService(
        IBookmarkRepository bookmarkRepository,
        IPostRepository postRepository,
        IPostService postService,
        ICommentService commentService)
    {
        _bookmarkRepository = bookmarkRepository;
        _postRepository = postRepository;
        _postService = postService;
        _commentService = commentService;
    }

    public async Task<UserBookmarksPageResponse> GetByUserIdAsync(
        Guid userId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var cursor = CursorCodec.Decode(pagination.Cursor);
        var posts = await _postService.GetBookmarkedByUserIdAsync(
            userId,
            pagination.Limit,
            cursor,
            cancellationToken,
            currentUserId);
        var comments = await _commentService.GetBookmarkedByUserIdAsync(
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
    
    public async Task<bool> AddBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(
            postId,
            cancellationToken);
        
        if (post == null)
        {
            return false;
        }
        
        var existingBookmark = await _bookmarkRepository.GetByUserAndPostId(
            userId,
            postId,
            cancellationToken);

        if (existingBookmark is not null)
        {
            return false;
        }

        var bookmark = new PostBookmark
        {
            UserId = userId,
            PostId = postId
        };

        try
        {
            await _bookmarkRepository.AddAsync(bookmark, cancellationToken);
        }
        catch (Exception exeption) when (IsDuplicateWriteException(exeption))
        {
            return false;
        }
        
        return true;
    }

    public async Task<bool> RemoveBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken = default)
    {
        var existingBookmark = await _bookmarkRepository.GetByUserAndPostId(
            userId,
            postId,
            cancellationToken);

        if (existingBookmark is null)
        {
            return false;
        }

        await _bookmarkRepository.DeleteAsync(existingBookmark, cancellationToken);
        
        return true;
    }
    
    private static bool IsDuplicateWriteException(Exception exception)
    {
        return exception.GetType().Name == "DbUpdateException";
    }

    private sealed record BookmarkedItem(
        DateTimeOffset ActionAt,
        Guid Id,
        PostResponse? Post,
        CommentResponse? Comment);
}
