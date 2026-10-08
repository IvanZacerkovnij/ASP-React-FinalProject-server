using NSubstitute;
using Threads.Application.Exceptions;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Comments;

public sealed class CommentMediaManagerTests
{
    private readonly CommentServiceTestContext _context = new();

    [Fact]
    public async Task ApplyAsync_WhenMediaIsValid_ReplacesCollectionAndPreservesRequestedOrder()
    {
        var authorId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateComment(authorId);
        var removedMedia = CreateMedia(Guid.NewGuid(), authorId);
        removedMedia.CommentId = comment.Id;
        removedMedia.SortOrder = 5;
        comment.Media.Add(removedMedia);
        var first = CreateMedia(Guid.NewGuid(), authorId);
        var second = CreateMedia(Guid.NewGuid(), authorId);
        _context.MediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([first, second]);

        await _context.MediaManager.ApplyAsync(comment, authorId, [second.Id, first.Id]);

        Assert.Equal([second.Id, first.Id], comment.Media.Select(media => media.Id));
        Assert.Equal([0, 1], comment.Media.Select(media => media.SortOrder));
        Assert.All(comment.Media, media => Assert.Equal(comment.Id, media.CommentId));
        Assert.Null(removedMedia.CommentId);
    }

    [Fact]
    public async Task ApplyAsync_WhenMediaIsUnchanged_DoesNotReloadOrReattachMedia()
    {
        var authorId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateComment(authorId);
        var first = CreateMedia(Guid.NewGuid(), authorId);
        first.CommentId = comment.Id;
        first.SortOrder = 0;
        var second = CreateMedia(Guid.NewGuid(), authorId);
        second.CommentId = comment.Id;
        second.SortOrder = 1;
        comment.Media.Add(first);
        comment.Media.Add(second);

        await _context.MediaManager.ApplyAsync(comment, authorId, [first.Id, second.Id]);

        await _context.MediaRepository.DidNotReceive().GetByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            Arg.Any<CancellationToken>());
        Assert.Same(first, comment.Media.ElementAt(0));
        Assert.Same(second, comment.Media.ElementAt(1));
    }

    [Fact]
    public async Task ApplyAsync_WhenMediaIsAttachedToPost_ThrowsConflictException()
    {
        var authorId = Guid.NewGuid();
        var comment = CommentServiceTestContext.CreateComment(authorId);
        var media = CreateMedia(Guid.NewGuid(), authorId);
        media.PostId = Guid.NewGuid();
        _context.MediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _context.MediaManager.ApplyAsync(comment, authorId, [media.Id]));
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
