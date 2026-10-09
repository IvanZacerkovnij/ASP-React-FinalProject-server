using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Quotes;
using Threads.Application.DTOs.Users;
using Threads.Application.DTOs.Search;
using Threads.Application.Exceptions;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Services.Posts;

public class PostQueryServiceTests
{
    private readonly PostServiceTestContext _context = new();

    [Fact]
    public async Task GetFeedAsync_RequestsTenRecommendedPostsAndPassesViewerAndCancellation()
    {
        var currentUserId = Guid.NewGuid();
        var posts = new[]
        {
            PostServiceTestContext.CreateSummary(),
            PostServiceTestContext.CreateSummary()
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _context.RecommendationService
            .GetFeedAsync(currentUserId, 10, cancellationToken)
            .Returns(posts.Select(_context.ResponseFactory.Create).ToArray());

        var result = await _context.QueryService.GetFeedAsync(cancellationToken, currentUserId);

        Assert.Equal(posts.Select(post => post.Id), result.Select(post => post.Id));
        await _context.RecommendationService.Received(1).GetFeedAsync(currentUserId, 10, cancellationToken);
    }

    [Fact]
    public async Task GetByAuthorIdAsync_WhenPostQuotesComment_ReturnsCurrentCommentAndVersionState()
    {
        var capturedVersionId = Guid.NewGuid();
        var currentVersionId = Guid.NewGuid();
        var comment = new CommentSummaryReadModel
        {
            Id = Guid.NewGuid(),
            VersionId = currentVersionId,
            PostId = Guid.NewGuid(),
            Content = "updated comment",
            Author = new UserSummaryReadModel
            {
                Id = Guid.NewGuid(),
                Username = "comment-author"
            }
        };
        var source = PostServiceTestContext.CreateSummary();
        source = new PostSummaryReadModel
        {
            Id = source.Id,
            VersionId = source.VersionId,
            Content = source.Content,
            Author = source.Author,
            CreatedAt = source.CreatedAt,
            Quote = new QuoteReadModel
            {
                TargetType = ContentTargetType.Comment,
                TargetId = comment.Id,
                TargetVersionId = capturedVersionId
            }
        };
        _context.PostRepository
            .GetByAuthorIdAsync(source.Author.Id, 20, null, null, Arg.Any<CancellationToken>())
            .Returns([source]);
        _context.CommentRepository
            .GetSummaryByIdAsync(comment.Id, null, Arg.Any<CancellationToken>())
            .Returns(comment);

        var result = Assert.Single((await _context.QueryService.GetByAuthorIdAsync(source.Author.Id, new CursorPageRequest())).Items);

        Assert.NotNull(result.Quote);
        Assert.True(result.Quote.HasNewVersion);
        Assert.Equal(capturedVersionId, result.Quote.TargetVersionId);
        var target = Assert.IsType<CommentResponse>(result.Quote.Target);
        Assert.Equal(currentVersionId, target.VersionId);
    }

    [Fact]
    public async Task GetByAuthorIdAsync_WhenPostQuotesPost_DoesNotExpandNestedQuote()
    {
        var target = CreateContent();
        target = new PostContentReadModel
        {
            Id = target.Id,
            VersionId = Guid.NewGuid(),
            AuthorId = target.AuthorId,
            Content = target.Content,
            CreatedAt = target.CreatedAt,
            UpdatedAt = target.UpdatedAt,
            Quote = new QuoteReadModel
            {
                TargetType = ContentTargetType.Post,
                TargetId = Guid.NewGuid(),
                TargetVersionId = Guid.NewGuid()
            }
        };
        var source = PostServiceTestContext.CreateSummary();
        source = new PostSummaryReadModel
        {
            Id = source.Id,
            VersionId = source.VersionId,
            Content = source.Content,
            Author = source.Author,
            CreatedAt = source.CreatedAt,
            Quote = new QuoteReadModel
            {
                TargetType = ContentTargetType.Post,
                TargetId = target.Id,
                TargetVersionId = target.VersionId
            }
        };
        _context.PostRepository
            .GetByAuthorIdAsync(source.Author.Id, 20, null, null, Arg.Any<CancellationToken>())
            .Returns([source]);
        _context.PostRepository
            .GetContentByIdAsync(target.Id, Arg.Any<CancellationToken>())
            .Returns(target);
        _context.PostRepository
            .GetEngagementByIdAsync(target.Id, null, Arg.Any<CancellationToken>())
            .Returns(new PostEngagementReadModel { UpdatedAt = target.UpdatedAt });
        _context.UserService
            .GetByIdAsync(target.AuthorId, Arg.Any<CancellationToken>(), null)
            .Returns(new UserResponse
            {
                Id = target.AuthorId,
                Username = "target-author"
            });

        var result = Assert.Single((await _context.QueryService.GetByAuthorIdAsync(source.Author.Id, new CursorPageRequest())).Items);

        Assert.NotNull(result.Quote);
        Assert.False(result.Quote.HasNewVersion);
        var quotedPost = Assert.IsType<Threads.Application.DTOs.Posts.Responses.PostResponse>(result.Quote.Target);
        Assert.Null(quotedPost.Quote);
    }

    [Fact]
    public async Task GetByAuthorIdAsync_WhenQuoteTargetIsUnavailable_PreservesQuoteMetadata()
    {
        var targetId = Guid.NewGuid();
        var targetVersionId = Guid.NewGuid();
        var source = PostServiceTestContext.CreateSummary();
        source = new PostSummaryReadModel
        {
            Id = source.Id,
            VersionId = source.VersionId,
            Content = source.Content,
            Author = source.Author,
            CreatedAt = source.CreatedAt,
            Quote = new QuoteReadModel
            {
                TargetType = ContentTargetType.Comment,
                TargetId = targetId,
                TargetVersionId = targetVersionId
            }
        };
        _context.PostRepository
            .GetByAuthorIdAsync(source.Author.Id, 20, null, null, Arg.Any<CancellationToken>())
            .Returns([source]);

        var result = Assert.Single((await _context.QueryService.GetByAuthorIdAsync(source.Author.Id, new CursorPageRequest())).Items);

        Assert.NotNull(result.Quote);
        Assert.Equal(targetId, result.Quote.TargetId);
        Assert.Equal(targetVersionId, result.Quote.TargetVersionId);
        Assert.False(result.Quote.HasNewVersion);
        Assert.Null(result.Quote.Target);
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
    public async Task SearchAsync_WithAllFilters_ForwardsValuesViewerAndBuildsPage()
    {
        var currentUserId = Guid.NewGuid();
        var posts = new[]
        {
            PostServiceTestContext.CreateSummary(),
            PostServiceTestContext.CreateSummary()
        };
        var fromDate = new DateOnly(2026, 1, 1);
        var toDate = new DateOnly(2026, 1, 2);
        _context.PostRepository
            .SearchAsync(
                "query",
                "following",
                "near",
                "exact phrase",
                Arg.Is<IReadOnlyCollection<string>>(words => words.SequenceEqual(new[] { "one", "two" })),
                Arg.Is<IReadOnlyCollection<string>>(words => words.SequenceEqual(new[] { "bad", "worse" })),
                "author",
                1,
                2,
                3,
                fromDate,
                toDate,
                true,
                1,
                null,
                currentUserId,
                Arg.Any<CancellationToken>())
            .Returns(posts);

        var result = await _context.QueryService.SearchAsync(
            new SearchPostsRequest
            {
                Q = " query ",
                People = " FOLLOWING ",
                Location = " near ",
                ExactPhrase = " exact phrase ",
                AnyWords = " one  TWO ",
                ExcludeWords = " bad worse ",
                From = " Author ",
                MinReplies = 1,
                MinLikes = 2,
                MinReposts = 3,
                FromDate = fromDate,
                ToDate = toDate,
                HasMedia = true,
                Limit = 1
            },
            currentUserId: currentUserId);

        Assert.Equal(posts[0].Id, Assert.Single(result.Items).Id);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task SearchAsync_WithFilterOnly_CallsRepositoryWithoutQuery()
    {
        await _context.QueryService.SearchAsync(new SearchPostsRequest { HasMedia = false });

        await _context.PostRepository.Received(1).SearchAsync(
            null,
            null,
            null,
            null,
            Arg.Is<IReadOnlyCollection<string>>(words => words.Count == 0),
            Arg.Is<IReadOnlyCollection<string>>(words => words.Count == 0),
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            20,
            null,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WithInvalidFilters_ThrowsValidationError()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.QueryService.SearchAsync(new SearchPostsRequest { People = "everyone" }));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.QueryService.SearchAsync(new SearchPostsRequest { MinLikes = -1 }));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _context.QueryService.SearchAsync(new SearchPostsRequest
            {
                FromDate = new DateOnly(2026, 2, 1),
                ToDate = new DateOnly(2026, 1, 1)
            }));
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
