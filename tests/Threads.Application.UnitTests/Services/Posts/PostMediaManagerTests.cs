using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Posts;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Posts;

public class PostMediaManagerTests
{
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly PostMediaManager _manager;

    public PostMediaManagerTests()
    {
        _manager = new PostMediaManager(
            _mediaRepository,
            _objectStorageService,
            Substitute.For<ILogger<PostMediaManager>>());
    }

    [Fact]
    public async Task ApplyAsync_WhenMediaIdsContainDuplicates_ThrowsRequestValidationException()
    {
        var mediaId = Guid.NewGuid();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _manager.ApplyAsync(new Post(), Guid.NewGuid(), [mediaId, mediaId]));

        await _mediaRepository.DidNotReceive().GetByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_WhenAnyMediaIsMissing_ThrowsNotFoundException()
    {
        var mediaIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        _mediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([CreateMedia(mediaIds[0], Guid.NewGuid())]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _manager.ApplyAsync(new Post(), Guid.NewGuid(), mediaIds));
    }

    [Fact]
    public async Task ApplyAsync_WhenMediaBelongsToAnotherUser_ThrowsForbiddenException()
    {
        var media = CreateMedia(Guid.NewGuid(), Guid.NewGuid());
        _mediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _manager.ApplyAsync(new Post(), Guid.NewGuid(), [media.Id]));
    }

    [Fact]
    public async Task ApplyAsync_WhenMediaIsAttachedToAnotherPost_ThrowsConflictException()
    {
        var authorId = Guid.NewGuid();
        var post = new Post { Id = Guid.NewGuid(), AuthorId = authorId };
        var media = CreateMedia(Guid.NewGuid(), authorId);
        media.PostId = Guid.NewGuid();
        _mediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _manager.ApplyAsync(post, authorId, [media.Id]));
    }

    [Fact]
    public async Task ApplyAsync_WhenMediaIsValid_ReplacesCollectionAndPreservesRequestedOrder()
    {
        var authorId = Guid.NewGuid();
        var post = new Post { Id = Guid.NewGuid(), AuthorId = authorId };
        var removedMedia = CreateMedia(Guid.NewGuid(), authorId);
        removedMedia.PostId = post.Id;
        removedMedia.SortOrder = 5;
        post.Media.Add(removedMedia);
        var first = CreateMedia(Guid.NewGuid(), authorId);
        var second = CreateMedia(Guid.NewGuid(), authorId);
        _mediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        await _manager.ApplyAsync(post, authorId, [second.Id, first.Id]);

        Assert.Equal([second.Id, first.Id], post.Media.Select(media => media.Id));
        Assert.Equal([0, 1], post.Media.Select(media => media.SortOrder));
        Assert.All(post.Media, media => Assert.Equal(post.Id, media.PostId));
        Assert.Null(removedMedia.PostId);
        Assert.Equal(0, removedMedia.SortOrder);
    }

    [Fact]
    public async Task ApplyAsync_WhenMediaIsUnchanged_DoesNotReloadOrReattachMedia()
    {
        var authorId = Guid.NewGuid();
        var post = new Post { Id = Guid.NewGuid(), AuthorId = authorId };
        var first = CreateMedia(Guid.NewGuid(), authorId);
        first.PostId = post.Id;
        first.SortOrder = 0;
        var second = CreateMedia(Guid.NewGuid(), authorId);
        second.PostId = post.Id;
        second.SortOrder = 1;
        post.Media.Add(first);
        post.Media.Add(second);

        await _manager.ApplyAsync(post, authorId, [first.Id, second.Id]);

        await _mediaRepository.DidNotReceive().GetByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
        Assert.Same(first, post.Media.ElementAt(0));
        Assert.Same(second, post.Media.ElementAt(1));
    }

    [Fact]
    public async Task TryDeleteAsync_ContinuesDeletingAfterStorageFailure()
    {
        _objectStorageService
            .DeleteAsync("broken-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("storage failed")));

        await _manager.TryDeleteAsync(["broken-key", "valid-key"]);

        await _objectStorageService.Received(1).DeleteAsync(
            "broken-key",
            Arg.Any<CancellationToken>());
        await _objectStorageService.Received(1).DeleteAsync(
            "valid-key",
            Arg.Any<CancellationToken>());
    }

    private static MediaEntity CreateMedia(Guid id, Guid uploaderId)
    {
        return new MediaEntity
        {
            Id = id,
            UploadedByUserId = uploaderId,
            StorageKey = $"media/{id:N}.jpg",
            FileName = "image.jpg",
            ContentType = "image/jpeg",
            Type = MediaType.Image,
            SizeInBytes = 100
        };
    }
}
