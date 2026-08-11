using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class BookmarkService: IBookmarkService
{
    private readonly IBookmarkRepository _bookmarkRepository;
    private readonly IPostRepository _postRepository;

    public BookmarkService(IBookmarkRepository bookmarkRepository,
        IPostRepository postRepository)
    {
        _bookmarkRepository = bookmarkRepository;
        _postRepository = postRepository;
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

        var bookmark = new Bookmark()
        {
            UserId = userId,
            PostId = postId,
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
}