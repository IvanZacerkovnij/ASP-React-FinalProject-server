using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Media;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Media;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Media;

public class MediaServiceTests
{
    private readonly IMediaRepository _mediaRepository = Substitute.For<IMediaRepository>();
    private readonly IMediaProcessingService _mediaProcessingService =
        Substitute.For<IMediaProcessingService>();
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly MediaService _service;

    public MediaServiceTests()
    {
        _objectStorageService
            .GetReadUrl(Arg.Any<string>())
            .Returns(callInfo => $"https://cdn.example/{callInfo.Arg<string>()}");
        _service = new MediaService(
            _mediaRepository,
            _mediaProcessingService,
            _objectStorageService,
            Substitute.For<ILogger<MediaService>>());
    }

    [Fact]
    public async Task GetUrlAsync_WhenMediaDoesNotExist_ReturnsNull()
    {
        var result = await _service.GetUrlAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
        _objectStorageService.DidNotReceive().GetReadUrl(Arg.Any<string>());
    }

    [Fact]
    public async Task GetUrlAsync_WhenUnattachedMediaBelongsToAnotherUser_ReturnsNull()
    {
        var media = CreateMedia();
        _mediaRepository
            .GetByIdAsync(media.Id, Arg.Any<CancellationToken>())
            .Returns(media);

        var result = await _service.GetUrlAsync(media.Id, Guid.NewGuid());

        Assert.Null(result);
        _objectStorageService.DidNotReceive().GetReadUrl(Arg.Any<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetUrlAsync_WhenMediaIsAccessible_ReturnsReadUrl(bool attachedToPost)
    {
        var media = CreateMedia();
        media.PostId = attachedToPost ? Guid.NewGuid() : null;
        _mediaRepository
            .GetByIdAsync(media.Id, Arg.Any<CancellationToken>())
            .Returns(media);

        var result = await _service.GetUrlAsync(
            media.Id,
            attachedToPost ? Guid.NewGuid() : media.UploadedByUserId);

        Assert.NotNull(result);
        Assert.Equal(media.Id, result.Id);
        Assert.Equal($"https://cdn.example/{media.StorageKey}", result.Url);
    }

    [Theory]
    [InlineData("", "image/png", 1, "File name is required.")]
    [InlineData("image.png", "", 1, "Content type is required.")]
    [InlineData("image.png", "image/png", 0, "File must not be empty.")]
    [InlineData("image.txt", "text/plain", 1, "Unsupported media content type.")]
    [InlineData("image.png", "image/png", 10485761, "File is too large.")]
    [InlineData("video.mp4", "video/mp4", 104857601, "File is too large.")]
    public async Task UploadAsync_WhenInputIsInvalid_ThrowsRequestValidationException(
        string fileName,
        string contentType,
        long sizeInBytes,
        string expectedMessage)
    {
        await using var content = new MemoryStream([1]);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.UploadAsync(
                Guid.NewGuid(),
                content,
                fileName,
                contentType,
                sizeInBytes));

        Assert.Equal(expectedMessage, exception.Message);
        await _mediaProcessingService.DidNotReceive().ProcessAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WhenImageIsValid_UploadsAndPersistsMedia()
    {
        var userId = Guid.NewGuid();
        await using var content = new MemoryStream([1, 2, 3]);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        MediaEntity? addedMedia = null;
        _mediaProcessingService
            .ProcessAsync(Arg.Any<string>(), "image/png", cancellationToken)
            .Returns(new MediaProcessingResult { Width = 640, Height = 480 });
        _mediaRepository
            .AddAsync(
                Arg.Do<MediaEntity>(media => addedMedia = media),
                cancellationToken)
            .Returns(Task.CompletedTask);

        var result = await _service.UploadAsync(
            userId,
            content,
            "../unsafe/PHOTO.PNG",
            "image/png",
            content.Length,
            cancellationToken);

        Assert.NotNull(addedMedia);
        Assert.Equal(userId, addedMedia.UploadedByUserId);
        Assert.Equal(MediaType.Image, addedMedia.Type);
        Assert.Equal("PHOTO.PNG", addedMedia.FileName);
        Assert.Equal(640, addedMedia.Width);
        Assert.Equal(480, addedMedia.Height);
        Assert.StartsWith($"users/{userId}/images/", addedMedia.StorageKey);
        Assert.EndsWith(".png", addedMedia.StorageKey);
        Assert.Equal("image", result.Type);
        Assert.Equal(result.Url, result.ThumbnailUrl);
        Assert.Equal(addedMedia.StorageKey, result.StorageKey);
        await _objectStorageService.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            addedMedia.StorageKey,
            "image/png",
            cancellationToken);
    }

    [Fact]
    public async Task UploadAsync_WhenVideoIsProcessed_UsesProcessedFileAndThumbnail()
    {
        var processedPath = Path.Combine(Path.GetTempPath(), $"processed-{Guid.NewGuid():N}.mp4");
        var thumbnailPath = Path.Combine(Path.GetTempPath(), $"thumbnail-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(processedPath, [1, 2, 3, 4]);
        await File.WriteAllBytesAsync(thumbnailPath, [5, 6]);
        await using var content = new MemoryStream([1, 2, 3]);
        var uploadedKeys = new List<string>();
        _mediaProcessingService
            .ProcessAsync(Arg.Any<string>(), "video/quicktime", Arg.Any<CancellationToken>())
            .Returns(new MediaProcessingResult
            {
                ProcessedFilePath = processedPath,
                ThumbnailFilePath = thumbnailPath,
                OutputContentType = "video/mp4",
                OutputSizeInBytes = 4,
                Width = 1920,
                Height = 1080,
                DurationSeconds = 12.5
            });
        _objectStorageService
            .UploadAsync(
                Arg.Any<Stream>(),
                Arg.Do<string>(key => uploadedKeys.Add(key)),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _service.UploadAsync(
            Guid.NewGuid(),
            content,
            "clip.mov",
            "video/quicktime",
            content.Length);

        Assert.Equal("video", result.Type);
        Assert.Equal("video/mp4", result.MimeType);
        Assert.Equal("clip.mp4", result.FileName);
        Assert.Equal(4, result.SizeInBytes);
        Assert.Equal(12.5, result.Duration);
        Assert.NotNull(result.ThumbnailUrl);
        Assert.Equal(2, uploadedKeys.Count);
        Assert.Contains(uploadedKeys, key => key.Contains("/videos/", StringComparison.Ordinal));
        Assert.Contains(uploadedKeys, key => key.Contains("/video-thumbnails/", StringComparison.Ordinal));
        Assert.False(File.Exists(processedPath));
        Assert.False(File.Exists(thumbnailPath));
    }

    [Fact]
    public async Task UploadAsync_WhenPersistenceFails_DeletesUploadedObjectsAndRethrows()
    {
        await using var content = new MemoryStream([1, 2, 3]);
        string? uploadedKey = null;
        _mediaProcessingService
            .ProcessAsync(Arg.Any<string>(), "image/jpeg", Arg.Any<CancellationToken>())
            .Returns(new MediaProcessingResult());
        _objectStorageService
            .UploadAsync(
                Arg.Any<Stream>(),
                Arg.Do<string>(key => uploadedKey = key),
                "image/jpeg",
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _mediaRepository
            .AddAsync(Arg.Any<MediaEntity>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("database failed")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UploadAsync(
                Guid.NewGuid(),
                content,
                "photo.jpg",
                "image/jpeg",
                content.Length));

        Assert.Equal("database failed", exception.Message);
        Assert.NotNull(uploadedKey);
        await _objectStorageService.Received(1).DeleteAsync(
            uploadedKey,
            Arg.Any<CancellationToken>());
    }

    private static MediaEntity CreateMedia()
    {
        return new MediaEntity
        {
            Id = Guid.NewGuid(),
            StorageKey = "users/user/images/media.png",
            FileName = "media.png",
            ContentType = "image/png",
            Type = MediaType.Image,
            SizeInBytes = 100,
            UploadedByUserId = Guid.NewGuid()
        };
    }
}
