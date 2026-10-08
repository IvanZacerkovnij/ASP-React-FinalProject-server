using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;
using Threads.Application.Services.Common;

namespace Threads.Application.UnitTests.Services.Comments;

public sealed class CommentThreadServiceTests
{
    private readonly CommentServiceTestContext _context = new();

    [Fact]
    public async Task GetAsync_WhenCommentDoesNotExist_ReturnsNull()
    {
        var result = await _context.ThreadService.GetAsync(
            Guid.NewGuid(),
            new CursorPageRequest());

        Assert.Null(result);
        await _context.PostService.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<Guid?>());
    }

    [Fact]
    public async Task GetAsync_ReturnsThreadAndPaginatedRepliesWithViewerState()
    {
        var commentId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var target = CommentServiceTestContext.CreateSummary(id: commentId, postId: postId);
        var ancestors = new[]
        {
            CommentServiceTestContext.CreateSummary(postId: postId),
            CommentServiceTestContext.CreateSummary(postId: postId)
        };
        var firstReplyTime = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var replies = new[]
        {
            CommentServiceTestContext.CreateSummary(postId: postId, createdAt: firstReplyTime),
            CommentServiceTestContext.CreateSummary(postId: postId, createdAt: firstReplyTime.AddMinutes(1))
        };
        var post = new PostResponse
        {
            Id = postId,
            Content = "post",
            Author = new UserShortResponse
            {
                Id = Guid.NewGuid(),
                Username = "post-author"
            }
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _context.CommentRepository
            .GetSummaryByIdAsync(commentId, currentUserId, cancellationToken)
            .Returns(target);
        _context.PostService
            .GetByIdAsync(postId, cancellationToken, currentUserId)
            .Returns(post);
        _context.CommentRepository
            .GetAncestorsAsync(commentId, currentUserId, cancellationToken)
            .Returns(ancestors);
        _context.CommentRepository
            .GetRepliesAsync(commentId, 1, null, currentUserId, cancellationToken)
            .Returns(replies);

        var result = await _context.ThreadService.GetAsync(
            commentId,
            new CursorPageRequest { Limit = 1 },
            cancellationToken,
            currentUserId);

        Assert.NotNull(result);
        Assert.Equal(postId, result.Post.Id);
        Assert.Equal(ancestors.Select(ancestor => ancestor.Id), result.Ancestors.Select(ancestor => ancestor.Id));
        Assert.Equal(commentId, result.Target.Id);
        Assert.Equal(replies[0].Id, Assert.Single(result.Replies.Items).Id);
        Assert.True(result.Replies.HasMore);
        var nextCursor = CursorCodec.Decode(result.Replies.NextCursor);
        Assert.Equal(replies[0].CreatedAt, nextCursor?.CreatedAt);
        Assert.Equal(replies[0].Id, nextCursor?.Id);
        await _context.CommentRepository.Received(1).GetRepliesAsync(
            commentId,
            1,
            null,
            currentUserId,
            cancellationToken);
    }
}
