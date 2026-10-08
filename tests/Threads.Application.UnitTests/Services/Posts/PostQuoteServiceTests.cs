using NSubstitute;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Posts;
using Threads.Domain.Entities;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Services.Posts;

public sealed class PostQuoteServiceTests
{
    private readonly IPostRepository _postRepository =
        Substitute.For<IPostRepository>();
    
    private readonly ICommentRepository _commentRepository =
        Substitute.For<ICommentRepository>();

    private readonly PostQuoteService _service;

    public PostQuoteServiceTests()
    {
        _service = new PostQuoteService(
            _postRepository,
            _commentRepository);
    }

    [Fact]
    public async Task CreateAsync_WhenQuoteIsAbsent_ReturnsNull()
    {
        var result = await _service.CreateAsync(
            new Post(),
            new CreatePostRequest
            {
                Content = "Post"
            });

        Assert.Null(result);
    }
    
    [Fact]
    public async Task CreateAsync_WhenBothTargetsAreSet_ThrowsValidationException()
    {
        var request = new CreatePostRequest
        {
            Content = "Post",
            QuotedPostId = Guid.NewGuid(),
            QuotedCommentId = Guid.NewGuid(),
            QuotedTargetVersionId = Guid.NewGuid()
        };

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => _service.CreateAsync(new Post(), request));

        Assert.Equal(
            "Only one quoted target may be specified.",
            exception.Message);
    }
   
    [Fact]
    public async Task CreateAsync_WhenTargetVersionIsMissing_ThrowsValidationException()
    {
        var request = new CreatePostRequest
        {
            Content = "Post",
            QuotedPostId = Guid.NewGuid()
        };

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => _service.CreateAsync(new Post(), request));

        Assert.Equal(
            "Quoted target version is required.",
            exception.Message);
    }
    
    [Fact]
    public async Task CreateAsync_WhenPostVersionExists_ReturnsPostQuote()
    {
        var targetId = Guid.NewGuid();
        var targetVersionId = Guid.NewGuid();
        var sourcePost = new Post();

        _postRepository
            .VersionExistsAsync(
                targetId,
                targetVersionId,
                Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.CreateAsync(
            sourcePost,
            new CreatePostRequest
            {
                Content = "Post",
                QuotedPostId = targetId,
                QuotedTargetVersionId = targetVersionId
            });

        Assert.NotNull(result);
        Assert.Equal(sourcePost.Id, result.PostId);
        Assert.Same(sourcePost, result.Post);
        Assert.Equal(ContentTargetType.Post, result.TargetType);
        Assert.Equal(targetId, result.TargetId);
        Assert.Equal(targetVersionId, result.TargetVersionId);
    }
    
    [Fact]
    public async Task CreateAsync_WhenCommentVersionExists_ReturnsCommentQuote()
    {
        var targetId = Guid.NewGuid();
        var targetVersionId = Guid.NewGuid();
        var sourcePost = new Post();

        _commentRepository
            .VersionExistsAsync(
                targetId,
                targetVersionId,
                Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _service.CreateAsync(
            sourcePost,
            new CreatePostRequest
            {
                Content = "Post",
                QuotedCommentId = targetId,
                QuotedTargetVersionId = targetVersionId
            });

        Assert.NotNull(result);
        Assert.Equal(ContentTargetType.Comment, result.TargetType);
        Assert.Equal(targetId, result.TargetId);
        Assert.Equal(targetVersionId, result.TargetVersionId);
    }
}