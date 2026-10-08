using NSubstitute;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Services.Common;
using Threads.Application.DTOs.Posts.Models;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Services.Comments;

public class CommentQueryServiceTests
{
    private readonly CommentServiceTestContext _context = new();

    [Fact]
    public async Task GetByAuthorIdAsync_WhenResultsExceedLimit_ReturnsPageCursorAndPassesCurrentUserId()
    {
        var authorId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var firstCreatedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var comments = new[]
        {
            CommentServiceTestContext.CreateSummary(createdAt: firstCreatedAt),
            CommentServiceTestContext.CreateSummary(createdAt: firstCreatedAt.AddMinutes(-1))
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _context.CommentRepository
            .GetByAuthorIdAsync(authorId, 1, null, currentUserId, cancellationToken)
            .Returns(comments);

        var result = await _context.QueryService.GetByAuthorIdAsync(
            authorId,
            new CursorPageRequest { Limit = 1 },
            cancellationToken,
            currentUserId);

        Assert.Equal(comments[0].Id, Assert.Single(result.Items).Id);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
        var nextCursor = CursorCodec.Decode(result.NextCursor);
        Assert.Equal(comments[0].CreatedAt, nextCursor?.CreatedAt);
        Assert.Equal(comments[0].Id, nextCursor?.Id);
        await _context.CommentRepository.Received(1).GetByAuthorIdAsync(
            authorId,
            1,
            null,
            currentUserId,
            cancellationToken);
    }

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
        comment = new Threads.Application.DTOs.Comments.CommentSummaryReadModel
        {
            Id = comment.Id,
            VersionId = comment.VersionId,
            PostId = comment.PostId,
            Content = comment.Content,
            Author = comment.Author,
            LinkPreviewUrl = "https://example.com/article",
            LinkPreviewTitle = "Article",
            LinkPreviewImageUrl = "/image.jpg",
            CreatedAt = comment.CreatedAt
        };
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
        Assert.Equal("example.com", result.LinkPreview?.Domain);
        Assert.Equal("https://example.com/image.jpg", result.LinkPreview?.ImageUrl);
    }

    [Fact]
    public async Task GetByIdAsync_WithExtendedContent_MapsAttachmentsPollAndLocation()
    {
        var currentUserId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateSummary();
        comment = new Threads.Application.DTOs.Comments.CommentSummaryReadModel
        {
            Id = comment.Id,
            VersionId = comment.VersionId,
            PostId = comment.PostId,
            Content = comment.Content,
            Author = comment.Author,
            Media =
            [
                new PostMediaReadModel
                {
                    Id = Guid.NewGuid(),
                    StorageKey = "media/image.jpg",
                    FileName = "image.jpg",
                    ContentType = "image/jpeg",
                    Type = MediaType.Image,
                    SizeInBytes = 100
                }
            ],
            Poll = new PostPollSummaryReadModel
            {
                Id = Guid.NewGuid(),
                TotalVotes = 1,
                SelectedOptionId = Guid.NewGuid(),
                Options =
                [
                    new PostPollOptionSummaryReadModel
                    {
                        Id = Guid.NewGuid(),
                        Text = "First",
                        Position = 0,
                        VotesCount = 1
                    }
                ]
            },
            LocationPlaceId = "kyiv",
            LocationName = "Kyiv",
            LocationCountry = "Ukraine",
            CreatedAt = comment.CreatedAt
        };
        _context.ObjectStorageService.GetReadUrl("media/image.jpg").Returns("https://cdn/image.jpg");
        _context.CommentRepository
            .GetSummaryByIdAsync(comment.Id, currentUserId, Arg.Any<CancellationToken>())
            .Returns(comment);

        var result = await _context.QueryService.GetByIdAsync(
            comment.Id,
            currentUserId: currentUserId);

        Assert.NotNull(result);
        Assert.Equal("https://cdn/image.jpg", Assert.Single(result.Attachments).Url);
        Assert.Equal(comment.Id, result.Poll?.CommentId);
        Assert.True(result.Poll?.HasVotedByCurrentUser);
        Assert.Equal("Kyiv", result.Location?.Name);
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
