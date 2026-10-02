using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.Exceptions;
using Threads.Application.Services.Comments;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Comments;

public class CommentManagementServiceTests
{
    private readonly CommentServiceTestContext _context = new();
    private readonly CommentManagementService _service;

    public CommentManagementServiceTests()
    {
        _service = new CommentManagementService(
            _context.CommentRepository,
            _context.PostRepository,
            _context.QueryService,
            _context.Mapper);
    }

    [Fact]
    public async Task CreateAsync_WhenContentIsBlank_ThrowsRequestValidationException()
    {
        var request = new CreateCommentRequest
        {
            PostId = Guid.NewGuid(),
            Content = " "
        };

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.CreateAsync(Guid.NewGuid(), request));

        await _context.PostRepository.DidNotReceive().ExistsAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(Guid.NewGuid(), request));

        Assert.Equal("Post was not found.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenParentDoesNotExist_ThrowsNotFoundException()
    {
        var request = CreateRequest(parentCommentId: Guid.NewGuid());
        _context.PostRepository
            .ExistsAsync(request.PostId, Arg.Any<CancellationToken>())
            .Returns(true);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(Guid.NewGuid(), request));

        Assert.Equal("Parent comment was not found.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenParentBelongsToAnotherPost_ThrowsRequestValidationException()
    {
        var request = CreateRequest(parentCommentId: Guid.NewGuid());
        _context.PostRepository
            .ExistsAsync(request.PostId, Arg.Any<CancellationToken>())
            .Returns(true);
        _context.CommentRepository
            .GetPostIdByIdAsync(request.ParentCommentId!.Value, Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.CreateAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_PersistsTrimmedCommentAndReturnsResponse()
    {
        var authorId = Guid.NewGuid();
        var parentCommentId = Guid.NewGuid();
        var request = CreateRequest(parentCommentId);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        Comment? addedComment = null;
        _context.PostRepository.ExistsAsync(request.PostId, cancellationToken).Returns(true);
        _context.CommentRepository
            .GetPostIdByIdAsync(parentCommentId, cancellationToken)
            .Returns(request.PostId);
        _context.CommentRepository
            .AddAsync(
                Arg.Do<Comment>(comment => addedComment = comment),
                cancellationToken)
            .Returns(Task.CompletedTask);
        _context.CommentRepository
            .GetSummaryByIdAsync(Arg.Any<Guid>(), authorId, cancellationToken)
            .Returns(callInfo => CommentServiceTestContext.CreateSummary(
                callInfo.ArgAt<Guid>(0),
                request.PostId));

        var result = await _service.CreateAsync(authorId, request, cancellationToken);

        Assert.NotNull(addedComment);
        Assert.Equal(authorId, addedComment.AuthorId);
        Assert.Equal(request.PostId, addedComment.PostId);
        Assert.Equal(parentCommentId, addedComment.ParentCommentId);
        Assert.Equal("new comment", addedComment.Content);
        Assert.Equal(addedComment.Id, result.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenPersistedCommentCannotBeRead_ThrowsInvalidOperationException()
    {
        var request = CreateRequest();
        _context.PostRepository
            .ExistsAsync(request.PostId, Arg.Any<CancellationToken>())
            .Returns(true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(Guid.NewGuid(), request));

        Assert.Equal("Created comment was not found.", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenCommentDoesNotExist_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateCommentRequest { Content = "updated" }));
    }

    [Fact]
    public async Task UpdateAsync_WhenCurrentUserIsNotAuthor_ThrowsForbiddenException()
    {
        var comment = CommentServiceTestContext.CreateComment();
        _context.CommentRepository
            .GetByIdAsync(comment.Id, Arg.Any<CancellationToken>())
            .Returns(comment);

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.UpdateAsync(
            comment.Id,
            Guid.NewGuid(),
            new UpdateCommentRequest { Content = "updated" }));
    }

    [Fact]
    public async Task UpdateAsync_WhenContentIsBlank_ThrowsRequestValidationException()
    {
        var comment = CommentServiceTestContext.CreateComment();
        _context.CommentRepository
            .GetByIdAsync(comment.Id, Arg.Any<CancellationToken>())
            .Returns(comment);

        await Assert.ThrowsAsync<RequestValidationException>(() => _service.UpdateAsync(
            comment.Id,
            comment.AuthorId,
            new UpdateCommentRequest { Content = " " }));
    }

    [Fact]
    public async Task UpdateAsync_WhenRequestIsValid_UpdatesAndReturnsComment()
    {
        var comment = CommentServiceTestContext.CreateComment();
        _context.CommentRepository
            .GetByIdAsync(comment.Id, Arg.Any<CancellationToken>())
            .Returns(comment);
        _context.CommentRepository
            .GetSummaryByIdAsync(
                comment.Id,
                comment.AuthorId,
                Arg.Any<CancellationToken>())
            .Returns(CommentServiceTestContext.CreateSummary(comment.Id, comment.PostId));

        var result = await _service.UpdateAsync(
            comment.Id,
            comment.AuthorId,
            new UpdateCommentRequest { Content = "  updated comment  " });

        Assert.Equal("updated comment", comment.Content);
        Assert.NotNull(comment.UpdatedAt);
        Assert.Equal(comment.Id, result.Id);
        await _context.CommentRepository.Received(1).UpdateAsync(
            comment,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenCommentDoesNotExist_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_WhenCurrentUserIsNotAuthor_ThrowsForbiddenException()
    {
        var comment = CommentServiceTestContext.CreateComment();
        _context.CommentRepository
            .GetByIdAsync(comment.Id, Arg.Any<CancellationToken>())
            .Returns(comment);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.DeleteAsync(comment.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_WhenCurrentUserIsAuthor_DeletesComment()
    {
        var comment = CommentServiceTestContext.CreateComment();
        _context.CommentRepository
            .GetByIdAsync(comment.Id, Arg.Any<CancellationToken>())
            .Returns(comment);

        await _service.DeleteAsync(comment.Id, comment.AuthorId);

        await _context.CommentRepository.Received(1).DeleteAsync(
            comment,
            Arg.Any<CancellationToken>());
    }

    private static CreateCommentRequest CreateRequest(Guid? parentCommentId = null)
    {
        return new CreateCommentRequest
        {
            PostId = Guid.NewGuid(),
            ParentCommentId = parentCommentId,
            Content = "  new comment  "
        };
    }
}
