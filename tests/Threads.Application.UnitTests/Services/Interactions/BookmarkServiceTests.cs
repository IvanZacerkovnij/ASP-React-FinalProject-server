using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Services.Interactions;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Interactions;

public class BookmarkServiceTests
{
    private readonly InteractionTestContext _context = new();
    private readonly IBookmarkRepository _bookmarkRepository = Substitute.For<IBookmarkRepository>();
    private readonly BookmarkService _service;

    public BookmarkServiceTests()
    {
        _service = new BookmarkService(
            _bookmarkRepository,
            _context.PostRepository,
            _context.CommentRepository,
            _context.PostQueryService,
            _context.CommentQueryService);
    }

    [Fact]
    public async Task GetByUserIdAsync_MergesItemsByActionTimeAndBuildsNextCursor()
    {
        var userId = Guid.NewGuid();
        var newestComment = InteractionTestContext.CreateComment(DateTimeOffset.UtcNow);
        var middlePost = InteractionTestContext.CreatePost(DateTimeOffset.UtcNow.AddMinutes(-1));
        var olderComment = InteractionTestContext.CreateComment(DateTimeOffset.UtcNow.AddMinutes(-2));
        _context.PostRepository
            .GetBookmarkedByUserIdAsync(
                userId,
                2,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns([middlePost]);
        _context.CommentRepository
            .GetBookmarkedByUserIdAsync(
                userId,
                2,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns([newestComment, olderComment]);

        var result = await _service.GetByUserIdAsync(
            userId,
            new CursorPageRequest { Limit = 2 });

        Assert.Equal(middlePost.Id, Assert.Single(result.Posts).Id);
        Assert.Equal(newestComment.Id, Assert.Single(result.Comments).Id);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task AddPostBookmarkAsync_WhenPostDoesNotExist_ReturnsFalse()
    {
        var result = await _service.AddPostBookmarkAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        await _bookmarkRepository.DidNotReceive().TryAddAsync(
            Arg.Any<PostBookmark>(),
            Arg.Any<CancellationToken>());
        await _context.PostRepository.DidNotReceive().RecordViewAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddPostBookmarkAsync_WhenPostExists_RecordsViewEvenForDuplicate(bool added)
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        PostBookmark? addedBookmark = null;
        _context.PostRepository.ExistsAsync(postId, Arg.Any<CancellationToken>()).Returns(true);
        _bookmarkRepository
            .TryAddAsync(
                Arg.Do<PostBookmark>(bookmark => addedBookmark = bookmark),
                Arg.Any<CancellationToken>())
            .Returns(added);

        var result = await _service.AddPostBookmarkAsync(userId, postId, cancellationToken);

        Assert.Equal(added, result);
        Assert.NotNull(addedBookmark);
        Assert.Equal(userId, addedBookmark.UserId);
        Assert.Equal(postId, addedBookmark.PostId);
        await _context.PostRepository.Received(1).RecordViewAsync(postId, userId, cancellationToken);
    }

    [Fact]
    public async Task AddCommentBookmarkAsync_WhenCommentDoesNotExist_ReturnsFalse()
    {
        var result = await _service.AddCommentBookmarkAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        await _bookmarkRepository.DidNotReceive().TryAddAsync(
            Arg.Any<CommentBookmark>(),
            Arg.Any<CancellationToken>());
        await _context.CommentRepository.DidNotReceive().RecordViewAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddCommentBookmarkAsync_WhenCommentExists_RecordsViewEvenForDuplicate(bool added)
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        CommentBookmark? addedBookmark = null;
        _context.CommentRepository.ExistsAsync(commentId, Arg.Any<CancellationToken>()).Returns(true);
        _bookmarkRepository
            .TryAddAsync(
                Arg.Do<CommentBookmark>(bookmark => addedBookmark = bookmark),
                Arg.Any<CancellationToken>())
            .Returns(added);

        var result = await _service.AddCommentBookmarkAsync(userId, commentId, cancellationToken);

        Assert.Equal(added, result);
        Assert.NotNull(addedBookmark);
        Assert.Equal(userId, addedBookmark.UserId);
        Assert.Equal(commentId, addedBookmark.CommentId);
        await _context.CommentRepository.Received(1).RecordViewAsync(commentId, userId, cancellationToken);
    }

    [Fact]
    public async Task RemoveMethods_DelegateToMatchingRepositoryOperations()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        _bookmarkRepository
            .TryDeletePostAsync(userId, postId, Arg.Any<CancellationToken>())
            .Returns(true);
        _bookmarkRepository
            .TryDeleteCommentAsync(userId, commentId, Arg.Any<CancellationToken>())
            .Returns(false);

        Assert.True(await _service.RemovePostBookmarkAsync(userId, postId));
        Assert.False(await _service.RemoveCommentBookmarkAsync(userId, commentId));
    }
}
