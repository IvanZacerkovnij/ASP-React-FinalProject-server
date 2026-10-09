using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Posts;

public class PostManagementServiceTests
{
    private readonly PostServiceTestContext _context = new();
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();

    private readonly PostVersionFactory _versionFactory =
        new PostVersionFactory(new VersionSnapshotSerializer());
    private readonly ICommentRepository _commentRepository =
        Substitute.For<ICommentRepository>();
    private readonly PostManagementService _service;

    public PostManagementServiceTests()
    {
        var quoteService = new PostQuoteService(
            _context.PostRepository,
            _commentRepository);
        var mediaManager = new PostMediaManager(
            _mediaRepository,
            _context.ObjectStorageService,
            Substitute.For<ILogger<PostMediaManager>>());
        _service = new PostManagementService(
            _context.PostRepository,
            mediaManager,
            _context.QueryService,
            _context.ResponseFactory,
            _versionFactory,
            quoteService,
            _context.Cache,
            Substitute.For<ILogger<PostManagementService>>());
    }

    [Fact]
    public async Task CreateAsync_WhenRequestHasNoContent_ThrowsRequestValidationException()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.CreateAsync(Guid.NewGuid(), new CreatePostRequest()));

        await _context.PostRepository.DidNotReceive().AddAsync(
            Arg.Any<Post>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsValid_PersistsAndReturnsCreatedPost()
    {
        var authorId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        Post? addedPost = null;
        _context.PostRepository
            .AddAsync(
                Arg.Do<Post>(post => addedPost = post),
                cancellationToken)
            .Returns(Task.CompletedTask);
        _context.PostRepository
            .GetByIdAsync(Arg.Any<Guid>(), cancellationToken)
            .Returns(_ =>
            {
                if (addedPost is not null)
                {
                    var author = PostServiceTestContext.CreateUser();
                    author.Id = authorId;
                    addedPost.Author = author;
                }

                return addedPost;
            });

        var result = await _service.CreateAsync(
            authorId,
            new CreatePostRequest { Content = "  New post  " },
            cancellationToken);

        Assert.NotNull(addedPost);
        Assert.Equal(authorId, addedPost.AuthorId);
        Assert.Equal("New post", addedPost.Content);
        var version = Assert.Single(addedPost.Versions);
        Assert.Equal(addedPost.CurrentVersionId, version.Id);
        Assert.Contains("\"content\":\"New post\"", version.SnapshotJson);
        Assert.Equal(addedPost.Id, result.Id);
        Assert.Equal(addedPost.CurrentVersionId, result.VersionId);
        Assert.Contains($"users:profile:v1:{authorId:N}", _context.Cache.RemovedKeys);
    }

    [Fact]
    public async Task CreateAsync_WhenCreatedPostCannotBeRead_ThrowsInvalidOperationException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(
                Guid.NewGuid(),
                new CreatePostRequest { Content = "new post" }));

        Assert.Equal("Created post was not found.", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdatePostRequest { Content = "updated" }));
    }

    [Fact]
    public async Task UpdateAsync_WhenCurrentUserIsNotAuthor_ThrowsForbiddenException()
    {
        var post = PostServiceTestContext.CreatePost();
        _context.PostRepository
            .GetByIdAsync(post.Id, Arg.Any<CancellationToken>())
            .Returns(post);

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.UpdateAsync(
            post.Id,
            Guid.NewGuid(),
            new UpdatePostRequest { Content = "updated" }));

        await _context.PostRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Post>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenUpdateRemovesLastPayload_ThrowsRequestValidationException()
    {
        var post = PostServiceTestContext.CreatePost();
        _context.PostRepository
            .GetByIdAsync(post.Id, Arg.Any<CancellationToken>())
            .Returns(post);

        await Assert.ThrowsAsync<RequestValidationException>(() => _service.UpdateAsync(
            post.Id,
            post.AuthorId,
            new UpdatePostRequest { Content = " " }));

        await _context.PostRepository.DidNotReceive().UpdateAsync(
            Arg.Any<Post>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenRequestIsValid_UpdatesPostInvalidatesCacheAndPreservesViews()
    {
        var post = PostServiceTestContext.CreatePost();
        var previousVersionId = post.CurrentVersionId;
        var media = CreateMedia(post.AuthorId);
        _context.PostRepository
            .GetByIdAsync(post.Id, Arg.Any<CancellationToken>())
            .Returns(post);
        _mediaRepository
            .GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([media]);
        _context.PostRepository
            .GetViewCountsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [post.Id] = 12 });

        var result = await _service.UpdateAsync(
            post.Id,
            post.AuthorId,
            new UpdatePostRequest
            {
                Content = "  updated content  ",
                MediaIds = [media.Id],
                LinkPreview = new LinkPreviewRequest { Url = " https://example.com " }
            });

        Assert.Equal("updated content", post.Content);
        Assert.Equal("https://example.com", post.EmbedUrl);
        Assert.Same(media, Assert.Single(post.Media));
        Assert.NotNull(post.UpdatedAt);
        Assert.NotEqual(previousVersionId, post.CurrentVersionId);
        var version = Assert.Single(post.Versions);
        Assert.Equal(post.CurrentVersionId, version.Id);
        Assert.Contains("\"content\":\"updated content\"", version.SnapshotJson);
        Assert.Equal(12, result.ViewsCount);
        Assert.Equal(post.CurrentVersionId, result.VersionId);
        await _context.PostRepository.Received(1).UpdateAsync(
            post,
            Arg.Any<CancellationToken>());
        Assert.Contains($"posts:core:v1:{post.Id:N}", _context.Cache.RemovedKeys);
    }

    [Fact]
    public async Task UpdateAsync_WhenRemovalFlagsAreProvided_ClearsPollLocationAndLinkPreview()
    {
        var post = PostServiceTestContext.CreatePost();
        var poll = new Poll { PostId = post.Id };
        post.Poll = poll;
        post.LocationName = "Kyiv";
        post.EmbedUrl = "https://example.com";
        _context.PostRepository
            .GetByIdAsync(post.Id, Arg.Any<CancellationToken>())
            .Returns(post);

        await _service.UpdateAsync(
            post.Id,
            post.AuthorId,
            new UpdatePostRequest
            {
                RemovePoll = true,
                RemoveLocation = true,
                RemoveLinkPreview = true
            });

        Assert.Null(post.Poll);
        Assert.Null(post.LocationName);
        Assert.Null(post.EmbedUrl);
        _context.PostRepository.Received(1).RemovePoll(poll);
    }

    [Fact]
    public async Task DeleteAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_WhenCurrentUserIsNotAuthor_ThrowsForbiddenException()
    {
        var post = PostServiceTestContext.CreatePost();
        _context.PostRepository
            .GetByIdAsync(post.Id, Arg.Any<CancellationToken>())
            .Returns(post);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.DeleteAsync(post.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_WhenPostExists_DeletesPostMediaAndCaches()
    {
        var post = PostServiceTestContext.CreatePost();
        post.Media.Add(CreateMedia(post.AuthorId, "media-key", "thumbnail-key"));
        post.Media.Add(CreateMedia(post.AuthorId, "media-key", null));
        _context.PostRepository
            .GetByIdAsync(post.Id, Arg.Any<CancellationToken>())
            .Returns(post);

        await _service.DeleteAsync(post.Id, post.AuthorId);

        await _context.PostRepository.Received(1).DeleteAsync(
            post,
            Arg.Any<CancellationToken>());
        await _context.ObjectStorageService.Received(1).DeleteAsync(
            "media-key",
            Arg.Any<CancellationToken>());
        await _context.ObjectStorageService.Received(1).DeleteAsync(
            "thumbnail-key",
            Arg.Any<CancellationToken>());
        Assert.Contains($"posts:core:v1:{post.Id:N}", _context.Cache.RemovedKeys);
        Assert.Contains($"users:profile:v1:{post.AuthorId:N}", _context.Cache.RemovedKeys);
    }

    private static MediaEntity CreateMedia(
        Guid uploaderId,
        string? storageKey = null,
        string? thumbnailStorageKey = null)
    {
        return new MediaEntity
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = uploaderId,
            StorageKey = storageKey ?? $"media/{Guid.NewGuid():N}.jpg",
            ThumbnailStorageKey = thumbnailStorageKey,
            FileName = "image.jpg",
            ContentType = "image/jpeg",
            Type = MediaType.Image,
            SizeInBytes = 100
        };
    }
}
