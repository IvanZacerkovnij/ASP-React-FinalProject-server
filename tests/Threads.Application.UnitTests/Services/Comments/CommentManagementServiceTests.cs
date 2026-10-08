using System.Text.Json;
using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Comments;

public class CommentManagementServiceTests
{
    private readonly CommentServiceTestContext _context = new();
    private readonly CommentVersionFactory _versionFactory =
        new CommentVersionFactory(new VersionSnapshotSerializer());
    private readonly CommentManagementService _service;

    public CommentManagementServiceTests()
    {
        _service = new CommentManagementService(
            _context.CommentRepository,
            _context.PostRepository,
            _context.MediaManager,
            _context.QueryService,
            _versionFactory,
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
        request = new CreateCommentRequest
        {
            PostId = request.PostId,
            ParentCommentId = request.ParentCommentId,
            Content = request.Content,
            LinkPreview = new LinkPreviewRequest
            {
                Url = " https://example.com/article ",
                Title = " Article ",
                ImageUrl = " https://example.com/image.jpg "
            }
        };
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
        Assert.Equal("https://example.com/article", addedComment.LinkPreviewUrl);
        Assert.Equal("Article", addedComment.LinkPreviewTitle);
        Assert.Equal("https://example.com/image.jpg", addedComment.LinkPreviewImageUrl);
        var version = Assert.Single(addedComment.Versions);
        Assert.Equal(addedComment.CurrentVersionId, version.Id);
        Assert.Contains("\"content\":\"new comment\"", version.SnapshotJson);
        Assert.Equal(addedComment.Id, result.Id);
    }

    [Fact]
    public async Task CreateAsync_WithMediaPollAndLocation_PersistsExtendedContentAndVersion()
    {
        var authorId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var request = new CreateCommentRequest
        {
            PostId = Guid.NewGuid(),
            Content = "comment",
            MediaIds = [mediaId],
            Poll = new CreatePostPollRequest
            {
                Options = ["First", "Second"]
            },
            Location = new PostLocationRequest
            {
                Id = "kyiv",
                Name = " Kyiv ",
                Country = " Ukraine ",
                Latitude = 50.4501,
                Longitude = 30.5234
            }
        };
        var media = new MediaEntity
        {
            Id = mediaId,
            UploadedByUserId = authorId,
            StorageKey = "media/image.jpg",
            FileName = "image.jpg",
            ContentType = "image/jpeg",
            Type = MediaType.Image,
            SizeInBytes = 100
        };
        Comment? addedComment = null;
        _context.PostRepository.ExistsAsync(request.PostId, Arg.Any<CancellationToken>()).Returns(true);
        _context.MediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);
        _context.CommentRepository
            .AddAsync(Arg.Do<Comment>(comment => addedComment = comment), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _context.CommentRepository
            .GetSummaryByIdAsync(Arg.Any<Guid>(), authorId, Arg.Any<CancellationToken>())
            .Returns(callInfo => CommentServiceTestContext.CreateSummary(
                callInfo.ArgAt<Guid>(0),
                request.PostId));

        await _service.CreateAsync(authorId, request);

        Assert.NotNull(addedComment);
        Assert.Equal("Kyiv", addedComment.LocationName);
        Assert.Equal("Ukraine", addedComment.LocationCountry);
        Assert.Equal(mediaId, Assert.Single(addedComment.Media).Id);
        Assert.Equal(["First", "Second"], addedComment.Poll?.Options.Select(option => option.Text));
        var version = Assert.Single(addedComment.Versions);
        Assert.Contains(mediaId.ToString(), version.SnapshotJson);
        Assert.Contains("\"location\"", version.SnapshotJson);
        Assert.Contains("\"poll\"", version.SnapshotJson);
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
        comment.LinkPreviewUrl = "https://example.com/original";
        var previousVersionId = comment.CurrentVersionId;
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
        Assert.Equal("https://example.com/original", comment.LinkPreviewUrl);
        Assert.NotNull(comment.UpdatedAt);
        Assert.NotEqual(previousVersionId, comment.CurrentVersionId);
        var version = Assert.Single(comment.Versions);
        Assert.Equal(comment.CurrentVersionId, version.Id);
        Assert.Contains("\"content\":\"updated comment\"", version.SnapshotJson);
        Assert.Equal(comment.Id, result.Id);
        await _context.CommentRepository.Received(1).UpdateAsync(
            comment,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenLinkPreviewRemovalIsRequested_ClearsLinkPreview()
    {
        var comment = CommentServiceTestContext.CreateComment();
        comment.LinkPreviewUrl = "https://example.com/original";
        comment.LinkPreviewTitle = "Original";
        comment.LinkPreviewImageUrl = "https://example.com/image.jpg";
        _context.CommentRepository
            .GetByIdAsync(comment.Id, Arg.Any<CancellationToken>())
            .Returns(comment);
        _context.CommentRepository
            .GetSummaryByIdAsync(
                comment.Id,
                comment.AuthorId,
                Arg.Any<CancellationToken>())
            .Returns(CommentServiceTestContext.CreateSummary(comment.Id, comment.PostId));

        await _service.UpdateAsync(
            comment.Id,
            comment.AuthorId,
            new UpdateCommentRequest
            {
                Content = "updated",
                RemoveLinkPreview = true
            });

        Assert.Null(comment.LinkPreviewUrl);
        Assert.Null(comment.LinkPreviewTitle);
        Assert.Null(comment.LinkPreviewImageUrl);
    }

    [Fact]
    public async Task UpdateAsync_WhenRemovalFlagsAreProvided_ClearsPollAndLocation()
    {
        var comment = CommentServiceTestContext.CreateComment();
        var poll = new Poll { CommentId = comment.Id };
        comment.Poll = poll;
        comment.LocationName = "Kyiv";
        _context.CommentRepository
            .GetByIdAsync(comment.Id, Arg.Any<CancellationToken>())
            .Returns(comment);
        _context.CommentRepository
            .GetSummaryByIdAsync(comment.Id, comment.AuthorId, Arg.Any<CancellationToken>())
            .Returns(CommentServiceTestContext.CreateSummary(comment.Id, comment.PostId));

        await _service.UpdateAsync(
            comment.Id,
            comment.AuthorId,
            new UpdateCommentRequest
            {
                Content = "updated",
                RemovePoll = true,
                RemoveLocation = true
            });

        Assert.Null(comment.Poll);
        Assert.Null(comment.LocationName);
        _context.CommentRepository.Received(1).RemovePoll(poll);
    }

    [Fact]
    public void UpdateCommentRequest_DeserializesRemovalFlags()
    {
        const string json =
            """
            {
              "content": "updated",
              "removePoll": true,
              "removeLocation": true,
              "removeLinkPreview": true
            }
            """;

        var request = JsonSerializer.Deserialize<UpdateCommentRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.True(request.RemovePoll);
        Assert.True(request.RemoveLocation);
        Assert.True(request.RemoveLinkPreview);
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
