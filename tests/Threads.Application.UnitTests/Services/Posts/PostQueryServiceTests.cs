using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;

namespace Threads.Application.UnitTests.Services.Posts;

public class PostQueryServiceTests
{
    private readonly PostServiceTestContext _context = new();

    [Fact]
    public async Task GetFeedAsync_RequestsTenPostsAndMapsResponses()
    {
        var currentUserId = Guid.NewGuid();
        var posts = new[]
        {
            PostServiceTestContext.CreateSummary(),
            PostServiceTestContext.CreateSummary()
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _context.PostRepository
            .GetRandomAsync(10, currentUserId, cancellationToken)
            .Returns(posts);

        var result = await _context.QueryService.GetFeedAsync(cancellationToken, currentUserId);

        Assert.Equal(posts.Select(post => post.Id), result.Select(post => post.Id));
    }

    [Fact]
    public async Task GetByAuthorIdAsync_WhenResultsExceedLimit_ReturnsPageAndCursor()
    {
        var authorId = Guid.NewGuid();
        var posts = new[]
        {
            PostServiceTestContext.CreateSummary(DateTimeOffset.UtcNow),
            PostServiceTestContext.CreateSummary(DateTimeOffset.UtcNow.AddMinutes(-1))
        };
        _context.PostRepository
            .GetByAuthorIdAsync(
                authorId,
                1,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(posts);

        var result = await _context.QueryService.GetByAuthorIdAsync(
            authorId,
            new CursorPageRequest { Limit = 1 });

        Assert.Equal(posts[0].Id, Assert.Single(result.Items).Id);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task SearchAsync_WhenQueryIsBlank_ReturnsEmptyPage()
    {
        var result = await _context.QueryService.SearchAsync(" ", new CursorPageRequest());

        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
        await _context.PostRepository.DidNotReceive().SearchAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<CursorPosition?>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WhenQueryExceedsMaximumLength_ThrowsRequestValidationException()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.QueryService.SearchAsync(
                new string('x', 101),
                new CursorPageRequest()));
    }

    [Fact]
    public async Task SearchAsync_WhenQueryIsValid_NormalizesAndPaginatesResults()
    {
        var posts = new[]
        {
            PostServiceTestContext.CreateSummary(),
            PostServiceTestContext.CreateSummary()
        };
        _context.PostRepository
            .SearchAsync("query", 1, null, null, Arg.Any<CancellationToken>())
            .Returns(posts);

        var result = await _context.QueryService.SearchAsync(
            "  query  ",
            new CursorPageRequest { Limit = 1 });

        Assert.Single(result.Items);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task GetByIdAsync_WhenContentDoesNotExist_ReturnsNullAndInvalidatesCache()
    {
        var postId = Guid.NewGuid();

        var result = await _context.QueryService.GetByIdAsync(postId);

        Assert.Null(result);
        Assert.Contains($"posts:core:v1:{postId:N}", _context.Cache.RemovedKeys);
        await _context.PostRepository.DidNotReceive().GetEngagementByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenEngagementDoesNotExist_ReturnsNullAndInvalidatesCache()
    {
        var content = CreateContent();
        _context.PostRepository
            .GetContentByIdAsync(content.Id, Arg.Any<CancellationToken>())
            .Returns(content);

        var result = await _context.QueryService.GetByIdAsync(content.Id);

        Assert.Null(result);
        Assert.Contains($"posts:core:v1:{content.Id:N}", _context.Cache.RemovedKeys);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCachedContentIsStale_RefreshesCacheAndReturnsMergedResponse()
    {
        var currentUserId = Guid.NewGuid();
        var staleContent = CreateContent();
        var refreshedContent = new PostContentReadModel
        {
            Id = staleContent.Id,
            AuthorId = staleContent.AuthorId,
            Content = "refreshed content",
            CreatedAt = staleContent.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var engagement = new PostEngagementReadModel
        {
            UpdatedAt = refreshedContent.UpdatedAt,
            LikesCount = 4,
            ViewsCount = 9
        };
        var author = new UserResponse
        {
            Id = staleContent.AuthorId,
            Username = "author"
        };
        _context.PostRepository
            .GetContentByIdAsync(staleContent.Id, Arg.Any<CancellationToken>())
            .Returns(staleContent, refreshedContent);
        _context.PostRepository
            .GetEngagementByIdAsync(
                staleContent.Id,
                currentUserId,
                Arg.Any<CancellationToken>())
            .Returns(engagement);
        _context.UserService
            .GetByIdAsync(staleContent.AuthorId, Arg.Any<CancellationToken>(), null)
            .Returns(author);

        var result = await _context.QueryService.GetByIdAsync(
            staleContent.Id,
            currentUserId: currentUserId);

        Assert.NotNull(result);
        Assert.Equal("refreshed content", result.Content);
        Assert.Equal(4, result.LikesCount);
        Assert.Equal(9, result.ViewsCount);
        Assert.Same(
            refreshedContent,
            _context.Cache.Values[$"posts:core:v1:{staleContent.Id:N}"]);
    }

    [Fact]
    public async Task GetByIdAsync_WhenAuthorDoesNotExist_ReturnsNullAndInvalidatesCache()
    {
        var content = CreateContent();
        _context.PostRepository
            .GetContentByIdAsync(content.Id, Arg.Any<CancellationToken>())
            .Returns(content);
        _context.PostRepository
            .GetEngagementByIdAsync(content.Id, null, Arg.Any<CancellationToken>())
            .Returns(new PostEngagementReadModel { UpdatedAt = content.UpdatedAt });

        var result = await _context.QueryService.GetByIdAsync(content.Id);

        Assert.Null(result);
        Assert.Contains($"posts:core:v1:{content.Id:N}", _context.Cache.RemovedKeys);
    }

    [Fact]
    public async Task GetViewCountAsync_WhenRepositoryHasNoCount_ReturnsZero()
    {
        var postId = Guid.NewGuid();
        _context.PostRepository
            .GetViewCountsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int>());

        var result = await _context.QueryService.GetViewCountAsync(postId);

        Assert.Equal(0, result);
    }

    private static PostContentReadModel CreateContent()
    {
        return new PostContentReadModel
        {
            Id = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "cached content",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
    }
}
