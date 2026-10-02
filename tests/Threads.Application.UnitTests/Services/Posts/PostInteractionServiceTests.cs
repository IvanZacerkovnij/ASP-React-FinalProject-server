using NSubstitute;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Posts;

namespace Threads.Application.UnitTests.Services.Posts;

public class PostInteractionServiceTests
{
    private readonly IPostRepository _postRepository = Substitute.For<IPostRepository>();
    private readonly PostInteractionService _service;

    public PostInteractionServiceTests()
    {
        _service = new PostInteractionService(_postRepository);
    }

    [Fact]
    public async Task RecordViewAsync_WhenPostDoesNotExist_ReturnsNull()
    {
        var result = await _service.RecordViewAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task RecordViewAsync_WhenRepositoryReturnsCount_ReturnsViewResponse()
    {
        var postId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _postRepository
            .RecordViewAsync(postId, viewerId, cancellationToken)
            .Returns(15);

        var result = await _service.RecordViewAsync(postId, viewerId, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(postId, result.PostId);
        Assert.Equal(15, result.ViewsCount);
    }
}
