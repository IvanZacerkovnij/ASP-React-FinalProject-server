using NSubstitute;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Services.Comments;

namespace Threads.Application.UnitTests.Services.Comments;

public class CommentInteractionServiceTests
{
    private readonly ICommentRepository _commentRepository = Substitute.For<ICommentRepository>();
    private readonly CommentInteractionService _service;

    public CommentInteractionServiceTests()
    {
        _service = new CommentInteractionService(_commentRepository);
    }

    [Fact]
    public async Task RecordViewAsync_WhenCommentDoesNotExist_ReturnsNull()
    {
        var result = await _service.RecordViewAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task RecordViewAsync_WhenRepositoryReturnsCount_ReturnsViewResponse()
    {
        var commentId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        _commentRepository
            .RecordViewAsync(commentId, viewerId, Arg.Any<CancellationToken>())
            .Returns(9);

        var result = await _service.RecordViewAsync(commentId, viewerId);

        Assert.NotNull(result);
        Assert.Equal(commentId, result.CommentId);
        Assert.Equal(9, result.ViewsCount);
    }
}
