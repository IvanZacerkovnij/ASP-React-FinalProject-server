using AutoMapper;
using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Users;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Threads.Domain.Models.Versions;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Posts;

public sealed class PostVersionServiceTests
{
    [Fact]
    public async Task GetEditHistoryAsync_LoadsDependenciesInBatchesAndBuildsVersions()
    {
        var postRepository = Substitute.For<IPostRepository>();
        var commentRepository = Substitute.For<ICommentRepository>();
        var mediaRepository = Substitute.For<IMediaRepository>();
        var objectStorage = Substitute.For<IObjectStorageService>();
        var mapper = Substitute.For<IMapper>();
        var serializer = new VersionSnapshotSerializer();
        var userResponseFactory = new UserResponseFactory(objectStorage, mapper);
        var versionResponseFactory = new PostVersionResponseFactory(serializer, userResponseFactory);
        var commentVersionResponseFactory = new CommentVersionResponseFactory(
            serializer,
            userResponseFactory);
        var service = new PostVersionService(
            postRepository,
            commentRepository,
            mediaRepository,
            objectStorage,
            versionResponseFactory,
            commentVersionResponseFactory);
        var currentUserId = Guid.NewGuid();
        var post = CreatePostSummary();
        var quotedComment = CreateCommentSummary();
        var firstMedia = CreateMedia();
        var secondMedia = CreateMedia();
        var quotedCommentVersion = new CommentVersion
        {
            Id = Guid.NewGuid(),
            CommentId = quotedComment.Id,
            SnapshotJson = serializer.Serialize(new CommentVersionSnapshot
            {
                Content = "historical quoted comment"
            })
        };
        var firstVersion = CreateVersion(
            serializer,
            post.Id,
            "first",
            [firstMedia.Id, secondMedia.Id],
            quotedComment,
            quotedCommentVersion.Id);
        var secondVersion = CreateVersion(
            serializer,
            post.Id,
            "second",
            [secondMedia.Id],
            quotedComment,
            quotedCommentVersion.Id);

        postRepository.GetSummariesByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                currentUserId,
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ids = callInfo.ArgAt<IReadOnlyCollection<Guid>>(0);
                return ids.Contains(post.Id)
                    ? [post]
                    : Array.Empty<PostSummaryReadModel>();
            });
        postRepository.GetVersionsAsync(
                post.Id,
                Arg.Any<int>(),
                Arg.Any<CursorPosition?>(),
                Arg.Any<CancellationToken>())
            .Returns([firstVersion, secondVersion]);
        commentRepository.GetSummariesByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                currentUserId,
                Arg.Any<CancellationToken>())
            .Returns([quotedComment]);
        commentRepository.GetVersionsByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns([quotedCommentVersion]);
        mediaRepository.GetByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns([firstMedia, secondMedia]);
        objectStorage.GetReadUrl(Arg.Any<string>())
            .Returns(callInfo => $"https://cdn.example/{callInfo.Arg<string>()}");

        var result = await service.GetEditHistoryAsync(
            post.Id,
            new CursorPageRequest(),
            currentUserId: currentUserId);

        Assert.NotNull(result);
        Assert.Equal(ContentTargetType.Post, result.TargetType);
        Assert.Equal(post.Id, result.TargetId);
        Assert.Equal(["first", "second"], result.Versions.Select(version => version.Content));
        Assert.Equal([0, 1], result.Versions.First().Media.Select(media => media.SortOrder));
        var quotedTarget = Assert.IsType<CommentResponse>(result.Versions.First().Quote?.Target);
        Assert.Equal(quotedCommentVersion.Id, quotedTarget.VersionId);
        Assert.Equal("historical quoted comment", quotedTarget.Content);
        Assert.True(result.Versions.First().Quote?.HasNewVersion);
        await mediaRepository.Received(1).GetByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                ids.Count == 2 && ids.Contains(firstMedia.Id) && ids.Contains(secondMedia.Id)),
            Arg.Any<CancellationToken>());
        await commentRepository.Received(1).GetSummariesByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                ids.Count == 1 && ids.Contains(quotedComment.Id)),
            currentUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEditHistoryAsync_WhenPostDoesNotExist_ReturnsNull()
    {
        var context = CreateService();
        context.PostRepository.GetSummariesByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PostSummaryReadModel>());

        var result = await context.Service.GetEditHistoryAsync(
            Guid.NewGuid(),
            new CursorPageRequest());

        Assert.Null(result);
        await context.PostRepository.DidNotReceive().GetVersionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<CursorPosition?>(),
            Arg.Any<CancellationToken>());
    }

    private static (PostVersionService Service, IPostRepository PostRepository) CreateService()
    {
        var postRepository = Substitute.For<IPostRepository>();
        var commentRepository = Substitute.For<ICommentRepository>();
        var mediaRepository = Substitute.For<IMediaRepository>();
        var objectStorage = Substitute.For<IObjectStorageService>();
        var mapper = Substitute.For<IMapper>();
        var serializer = new VersionSnapshotSerializer();
        var userResponseFactory = new UserResponseFactory(objectStorage, mapper);

        return (
            new PostVersionService(
                postRepository,
                commentRepository,
                mediaRepository,
                objectStorage,
                new PostVersionResponseFactory(serializer, userResponseFactory),
                new CommentVersionResponseFactory(serializer, userResponseFactory)),
            postRepository);
    }

    private static PostSummaryReadModel CreatePostSummary()
    {
        return new PostSummaryReadModel
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Content = "current",
            Author = CreateAuthor(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static CommentSummaryReadModel CreateCommentSummary()
    {
        return new CommentSummaryReadModel
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            PostId = Guid.NewGuid(),
            Content = "quoted comment",
            Author = CreateAuthor(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static UserSummaryReadModel CreateAuthor()
    {
        return new UserSummaryReadModel
        {
            Id = Guid.NewGuid(),
            Username = "author"
        };
    }

    private static MediaEntity CreateMedia()
    {
        var id = Guid.NewGuid();

        return new MediaEntity
        {
            Id = id,
            StorageKey = $"media/{id}.jpg",
            FileName = $"{id}.jpg",
            ContentType = "image/jpeg",
            Type = MediaType.Image,
            SizeInBytes = 100,
            UploadedByUserId = Guid.NewGuid()
        };
    }

    private static PostVersion CreateVersion(
        VersionSnapshotSerializer serializer,
        Guid postId,
        string content,
        List<Guid> mediaIds,
        CommentSummaryReadModel quotedComment,
        Guid quotedCommentVersionId)
    {
        return new PostVersion
        {
            PostId = postId,
            SnapshotJson = serializer.Serialize(new PostVersionSnapshot
            {
                Content = content,
                MediaIds = mediaIds,
                Quote = new QuoteVersionSnapshot
                {
                    TargetType = ContentTargetType.Comment,
                    TargetId = quotedComment.Id,
                    TargetVersionId = quotedCommentVersionId
                }
            })
        };
    }
}
