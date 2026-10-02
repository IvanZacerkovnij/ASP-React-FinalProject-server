using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Services.Interactions;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Interactions;

public class RepostServiceTests
{
    private readonly InteractionTestContext _context = new();
    private readonly IRepostRepository _repostRepository = Substitute.For<IRepostRepository>();
    private readonly RepostService _service;

    public RepostServiceTests()
    {
        _service = new RepostService(
            _repostRepository,
            _context.PostRepository,
            _context.CommentRepository,
            _context.PostQueryService,
            _context.CommentQueryService);
    }

    [Fact]
    public async Task GetByUserIdAsync_MergesItemsByActionTimeAndBuildsNextCursor()
    {
        var userId = Guid.NewGuid();
        var newestPost = InteractionTestContext.CreatePost(DateTimeOffset.UtcNow);
        var middleComment = InteractionTestContext.CreateComment(DateTimeOffset.UtcNow.AddMinutes(-1));
        var olderPost = InteractionTestContext.CreatePost(DateTimeOffset.UtcNow.AddMinutes(-2));
        _context.PostRepository
            .GetRepostedByUserIdAsync(
                userId,
                2,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns([newestPost, olderPost]);
        _context.CommentRepository
            .GetRepostedByUserIdAsync(
                userId,
                2,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns([middleComment]);

        var result = await _service.GetByUserIdAsync(
            userId,
            new CursorPageRequest { Limit = 2 });

        Assert.Equal(newestPost.Id, Assert.Single(result.Posts).Id);
        Assert.Equal(middleComment.Id, Assert.Single(result.Comments).Id);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task AddPostRepostAsync_WhenPostDoesNotExist_ReturnsFalse()
    {
        var result = await _service.AddPostRepostAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        await _repostRepository.DidNotReceive().TryAddAsync(
            Arg.Any<PostRepost>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPostRepostAsync_WhenPostExists_ForwardsRepostToRepository()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        PostRepost? addedRepost = null;
        _context.PostRepository.ExistsAsync(postId, Arg.Any<CancellationToken>()).Returns(true);
        _repostRepository
            .TryAddAsync(
                Arg.Do<PostRepost>(repost => addedRepost = repost),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.AddPostRepostAsync(userId, postId);

        Assert.True(result);
        Assert.NotNull(addedRepost);
        Assert.Equal(userId, addedRepost.UserId);
        Assert.Equal(postId, addedRepost.PostId);
    }

    [Fact]
    public async Task AddCommentRepostAsync_WhenCommentDoesNotExist_ReturnsFalse()
    {
        var result = await _service.AddCommentRepostAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        await _repostRepository.DidNotReceive().TryAddAsync(
            Arg.Any<CommentRepost>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCommentRepostAsync_WhenCommentExists_ForwardsRepostToRepository()
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        CommentRepost? addedRepost = null;
        _context.CommentRepository.ExistsAsync(commentId, Arg.Any<CancellationToken>()).Returns(true);
        _repostRepository
            .TryAddAsync(
                Arg.Do<CommentRepost>(repost => addedRepost = repost),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.AddCommentRepostAsync(userId, commentId);

        Assert.True(result);
        Assert.NotNull(addedRepost);
        Assert.Equal(userId, addedRepost.UserId);
        Assert.Equal(commentId, addedRepost.CommentId);
    }

    [Fact]
    public async Task RemoveMethods_DelegateToMatchingRepositoryOperations()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        _repostRepository
            .TryDeletePostAsync(userId, postId, Arg.Any<CancellationToken>())
            .Returns(true);
        _repostRepository
            .TryDeleteCommentAsync(userId, commentId, Arg.Any<CancellationToken>())
            .Returns(false);

        Assert.True(await _service.RemovePostRepostAsync(userId, postId));
        Assert.False(await _service.RemoveCommentRepostAsync(userId, commentId));
    }
}
