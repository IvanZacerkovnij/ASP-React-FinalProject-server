using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.ScheduledPosts;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.ScheduledPosts;
using Threads.Application.UnitTests.Helpers;
using Threads.Application.Services.Posts;
using Threads.Application.Services.ScheduledPosts;
using Threads.Application.Services.Versions;
using Threads.Application.UnitTests.TestSupport;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.UnitTests.Services.ScheduledPosts;

public sealed class ScheduledPostServiceTests
{
    private readonly IScheduledPostRepository _repository = Substitute.For<IScheduledPostRepository>();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly VersionSnapshotSerializer _serializer = new();
    private readonly ScheduledPostService _service;

    public ScheduledPostServiceTests()
    {
        _service = new ScheduledPostService(
            _repository,
            new ScheduledPostMediaManager(_mediaRepository),
            new ScheduledPostResponseFactory(_objectStorageService),
            new PostVersionFactory(_serializer),
            new TestHybridCache(),
            Substitute.For<ILogger<ScheduledPostService>>());
    }

    [Fact]
    public async Task CreateAsync_WhenScheduledAtIsNotFuture_ThrowsRequestValidationException()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _service.CreateAsync(
            Guid.NewGuid(),
            new CreateScheduledPostRequest
            {
                Content = "post",
                ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            }));

        await _repository.DidNotReceive().AddAsync(
            Arg.Any<ScheduledPost>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReservesMediaAndReturnsNormalizedResponse()
    {
        var authorId = Guid.NewGuid();
        var media = TestEntityFactory.CreateMedia(authorId);
        var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);
        _mediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);
        _objectStorageService.GetReadUrl(media.StorageKey).Returns("https://cdn/image.jpg");
        ScheduledPost? addedScheduledPost = null;
        _repository
            .AddAsync(
                Arg.Do<ScheduledPost>(post => addedScheduledPost = post),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(
            authorId,
            new CreateScheduledPostRequest
            {
                Content = "  scheduled post  ",
                MediaIds = [media.Id],
                LinkPreview = new LinkPreviewRequest
                {
                    Url = " https://example.com/article ",
                    Title = " Article "
                },
                ScheduledAt = scheduledAt
            });

        Assert.NotNull(addedScheduledPost);
        Assert.Equal("scheduled post", addedScheduledPost.Content);
        Assert.Equal("https://example.com/article", addedScheduledPost.LinkPreviewUrl);
        Assert.Equal(addedScheduledPost.Id, media.ScheduledPostId);
        Assert.Equal(media.Id, Assert.Single(result.MediaIds));
        Assert.Equal("https://cdn/image.jpg", Assert.Single(result.Media).Url);
        Assert.Equal(scheduledAt, result.ScheduledAt);
    }

    [Fact]
    public async Task UpdateAsync_WhenCurrentUserIsNotAuthor_ThrowsForbiddenException()
    {
        var scheduledPost = TestEntityFactory.CreateScheduledPost(Guid.NewGuid());
        _repository.GetByIdAsync(scheduledPost.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledPost);

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.UpdateAsync(
            scheduledPost.Id,
            Guid.NewGuid(),
            new UpdateScheduledPostRequest { Content = "updated" }));
    }

    [Fact]
    public async Task UpdateAsync_ClearsLinkPreviewAndReschedulesPost()
    {
        var authorId = Guid.NewGuid();
        var scheduledPost = TestEntityFactory.CreateScheduledPost(
            authorId,
            linkPreviewUrl: "https://example.com/original");
        var newScheduledAt = DateTimeOffset.UtcNow.AddHours(2);
        _repository.GetByIdAsync(scheduledPost.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledPost);

        var result = await _service.UpdateAsync(
            scheduledPost.Id,
            authorId,
            new UpdateScheduledPostRequest
            {
                Content = " updated ",
                LinkPreview = null,
                ScheduledAt = newScheduledAt
            });

        Assert.Equal("updated", scheduledPost.Content);
        Assert.Null(scheduledPost.LinkPreviewUrl);
        Assert.Equal(newScheduledAt, result.ScheduledAt);
        Assert.NotNull(scheduledPost.UpdatedAt);
        await _repository.Received(1).UpdateAsync(
            scheduledPost,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_DetachesReservedMedia()
    {
        var authorId = Guid.NewGuid();
        var scheduledPostId = Guid.NewGuid();
        var media = TestEntityFactory.CreateMedia(
            authorId,
            scheduledPostId: scheduledPostId);
        var scheduledPost = TestEntityFactory.CreateScheduledPost(
            authorId,
            id: scheduledPostId,
            media: [media]);
        _repository.GetByIdAsync(scheduledPost.Id, Arg.Any<CancellationToken>())
            .Returns(scheduledPost);

        await _service.DeleteAsync(scheduledPost.Id, authorId);

        Assert.Null(media.ScheduledPostId);
        Assert.Empty(scheduledPost.Media);
        await _repository.Received(1).DeleteAsync(
            scheduledPost,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishDueAsync_CreatesPostTransfersMediaAndCreatesVersion()
    {
        var authorId = Guid.NewGuid();
        var scheduledPostId = Guid.NewGuid();
        var media = TestEntityFactory.CreateMedia(
            authorId,
            scheduledPostId: scheduledPostId);
        var scheduledPost = TestEntityFactory.CreateScheduledPost(
            authorId,
            scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            id: scheduledPostId,
            media: [media]);
        _repository
            .GetDueAsync(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns([scheduledPost]);
        Post? publishedPost = null;
        _repository
            .PublishAsync(
                scheduledPost,
                Arg.Do<Post>(post => publishedPost = post),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var publishedCount = await _service.PublishDueAsync();

        Assert.Equal(1, publishedCount);
        Assert.NotNull(publishedPost);
        Assert.Equal(authorId, publishedPost.AuthorId);
        Assert.Equal(media.Id, Assert.Single(publishedPost.Media).Id);
        Assert.Equal(publishedPost.Id, media.PostId);
        Assert.Null(media.ScheduledPostId);
        var version = Assert.Single(publishedPost.Versions);
        var snapshot = _serializer.Deserialize<PostVersionSnapshot>(version.SnapshotJson);
        Assert.Equal([media.Id], snapshot.MediaIds);
    }

    [Fact]
    public async Task CreateAsync_WhenMediaIsAlreadyAttached_ThrowsConflictException()
    {
        var authorId = Guid.NewGuid();
        var media = TestEntityFactory.CreateMedia(
            authorId,
            commentId: Guid.NewGuid());
        _mediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(
            authorId,
            new CreateScheduledPostRequest
            {
                Content = "post",
                MediaIds = [media.Id],
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(1)
            }));
    }

}
