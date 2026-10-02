using NSubstitute;
using Threads.Application.DTOs.Pagination;

namespace Threads.Application.UnitTests.Services.Comments;

public class CommentQueryServiceTests
{
    private readonly CommentServiceTestContext _context = new();

    [Fact]
    public async Task GetByPostIdAsync_WhenResultsExceedLimit_ReturnsPageAndCursor()
    {
        var postId = Guid.NewGuid();
        var comments = new[]
        {
            CommentServiceTestContext.CreateSummary(postId: postId, createdAt: DateTimeOffset.UtcNow),
            CommentServiceTestContext.CreateSummary(postId: postId, createdAt: DateTimeOffset.UtcNow.AddMinutes(-1))
        };
        _context.CommentRepository
            .GetByPostIdAsync(postId, 1, null, null, Arg.Any<CancellationToken>())
            .Returns(comments);

        var result = await _context.QueryService.GetByPostIdAsync(
            postId,
            new CursorPageRequest { Limit = 1 });

        Assert.Equal(comments[0].Id, Assert.Single(result.Items).Id);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCommentDoesNotExist_ReturnsNull()
    {
        var result = await _context.QueryService.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCommentExists_MapsResponse()
    {
        var currentUserId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateSummary();
        comment.ActionAt = DateTimeOffset.UtcNow;
        _context.CommentRepository
            .GetSummaryByIdAsync(comment.Id, currentUserId, Arg.Any<CancellationToken>())
            .Returns(comment);

        var result = await _context.QueryService.GetByIdAsync(
            comment.Id,
            currentUserId: currentUserId);

        Assert.NotNull(result);
        Assert.Equal(comment.Id, result.Id);
        Assert.Equal(comment.ActionAt, result.ActionAt);
    }

    [Fact]
    public async Task GetBookmarkedByUserIdAsync_MapsRepositoryResults()
    {
        var userId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateSummary();
        _context.CommentRepository
            .GetBookmarkedByUserIdAsync(
                userId,
                10,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns([comment]);

        var result = await _context.QueryService.GetBookmarkedByUserIdAsync(userId, 10);

        Assert.Equal(comment.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetLikedByUserIdAsync_MapsRepositoryResults()
    {
        var userId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateSummary();
        _context.CommentRepository
            .GetLikedByUserIdAsync(
                userId,
                10,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns([comment]);

        var result = await _context.QueryService.GetLikedByUserIdAsync(userId, 10);

        Assert.Equal(comment.Id, Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetRepostedByUserIdAsync_MapsRepositoryResults()
    {
        var userId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateSummary();
        _context.CommentRepository
            .GetRepostedByUserIdAsync(
                userId,
                10,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns([comment]);

        var result = await _context.QueryService.GetRepostedByUserIdAsync(userId, 10);

        Assert.Equal(comment.Id, Assert.Single(result).Id);
    }
}
