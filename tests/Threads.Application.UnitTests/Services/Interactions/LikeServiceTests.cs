using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Services.Interactions;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Interactions;

public class LikeServiceTests
{
    private readonly InteractionTestContext _context = new();
    private readonly ILikeRepository _likeRepository = Substitute.For<ILikeRepository>();
    private readonly LikeService _service;

    public LikeServiceTests()
    {
        _service = new LikeService(
            _likeRepository,
            _context.PostRepository,
            _context.CommentRepository,
            _context.PostQueryService,
            _context.CommentQueryService);
    }

    [Fact]
    public async Task GetByUserIdAsync_MergesItemsByActionTimeAndBuildsNextCursor()
    {
        var userId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var newestPost = InteractionTestContext.CreatePost(DateTimeOffset.UtcNow);
        var middleComment = InteractionTestContext.CreateComment(DateTimeOffset.UtcNow.AddMinutes(-1));
        var olderPost = InteractionTestContext.CreatePost(DateTimeOffset.UtcNow.AddMinutes(-2));
        var itemWithoutAction = InteractionTestContext.CreateComment(null);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _context.PostRepository
            .GetLikedByUserIdAsync(userId, 2, null, currentUserId, cancellationToken)
            .Returns([newestPost, olderPost]);
        _context.CommentRepository
            .GetLikedByUserIdAsync(userId, 2, null, currentUserId, cancellationToken)
            .Returns([middleComment, itemWithoutAction]);

        var result = await _service.GetByUserIdAsync(
            userId,
            new CursorPageRequest { Limit = 2 },
            cancellationToken,
            currentUserId);

        Assert.Equal(newestPost.Id, Assert.Single(result.Posts).Id);
        Assert.Equal(middleComment.Id, Assert.Single(result.Comments).Id);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task AddPostLikeAsync_WhenPostDoesNotExist_ReturnsFalse()
    {
        var result = await _service.AddPostLikeAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        await _likeRepository.DidNotReceive().TryAddAsync(
            Arg.Any<PostLike>(),
            Arg.Any<CancellationToken>());
        await _context.PostRepository.DidNotReceive().RecordViewAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddPostLikeAsync_WhenPostExists_RecordsViewEvenForDuplicate(bool added)
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        PostLike? addedLike = null;
        _context.PostRepository.ExistsAsync(postId, Arg.Any<CancellationToken>()).Returns(true);
        _likeRepository
            .TryAddAsync(
                Arg.Do<PostLike>(like => addedLike = like),
                Arg.Any<CancellationToken>())
            .Returns(added);

        var result = await _service.AddPostLikeAsync(userId, postId, cancellationToken);

        Assert.Equal(added, result);
        Assert.NotNull(addedLike);
        Assert.Equal(userId, addedLike.UserId);
        Assert.Equal(postId, addedLike.PostId);
        await _context.PostRepository.Received(1).RecordViewAsync(postId, userId, cancellationToken);
    }

    [Fact]
    public async Task AddCommentLikeAsync_WhenCommentDoesNotExist_ReturnsFalse()
    {
        var result = await _service.AddCommentLikeAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        await _likeRepository.DidNotReceive().TryAddAsync(
            Arg.Any<CommentLike>(),
            Arg.Any<CancellationToken>());
        await _context.CommentRepository.DidNotReceive().RecordViewAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddCommentLikeAsync_WhenCommentExists_RecordsViewEvenForDuplicate(bool added)
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        CommentLike? addedLike = null;
        _context.CommentRepository.ExistsAsync(commentId, Arg.Any<CancellationToken>()).Returns(true);
        _likeRepository
            .TryAddAsync(
                Arg.Do<CommentLike>(like => addedLike = like),
                Arg.Any<CancellationToken>())
            .Returns(added);

        var result = await _service.AddCommentLikeAsync(userId, commentId, cancellationToken);

        Assert.Equal(added, result);
        Assert.NotNull(addedLike);
        Assert.Equal(userId, addedLike.UserId);
        Assert.Equal(commentId, addedLike.CommentId);
        await _context.CommentRepository.Received(1).RecordViewAsync(commentId, userId, cancellationToken);
    }

    [Fact]
    public async Task RemoveMethods_DelegateToMatchingRepositoryOperations()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        _likeRepository
            .TryDeletePostAsync(userId, postId, Arg.Any<CancellationToken>())
            .Returns(true);
        _likeRepository
            .TryDeleteCommentAsync(userId, commentId, Arg.Any<CancellationToken>())
            .Returns(false);

        var postResult = await _service.RemovePostLikeAsync(userId, postId);
        var commentResult = await _service.RemoveCommentLikeAsync(userId, commentId);

        Assert.True(postResult);
        Assert.False(commentResult);
    }
}
