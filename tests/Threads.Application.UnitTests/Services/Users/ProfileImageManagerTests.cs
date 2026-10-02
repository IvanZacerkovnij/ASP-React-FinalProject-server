using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Users;

namespace Threads.Application.UnitTests.Services.Users;

public class ProfileImageManagerTests
{
    private readonly IObjectStorageService _objectStorageService = Substitute.For<IObjectStorageService>();
    private readonly ProfileImageManager _manager;

    public ProfileImageManagerTests()
    {
        _manager = new ProfileImageManager(
            _objectStorageService,
            Substitute.For<ILogger<ProfileImageManager>>());
    }

    [Theory]
    [InlineData(0, "image/png", "Avatar file must not be empty.")]
    [InlineData(10485761, "image/png", "Avatar file is too large.")]
    [InlineData(1, "image/gif", "Avatar content type is not supported.")]
    public async Task UploadAvatarAsync_WhenFileIsInvalid_ThrowsRequestValidationException(
        long sizeInBytes,
        string contentType,
        string expectedMessage)
    {
        await using var content = new MemoryStream([1]);
        var file = new UserFileUploadRequest
        {
            Content = content,
            FileName = "avatar.png",
            ContentType = contentType,
            SizeInBytes = sizeInBytes
        };

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _manager.UploadAvatarAsync(Guid.NewGuid(), file));

        Assert.Equal(expectedMessage, exception.Message);
        await _objectStorageService.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, "avatar")]
    [InlineData(false, "banner")]
    public async Task UploadMethods_WhenFileIsValid_UploadWithNormalizedObjectKey(
        bool isAvatar,
        string expectedKind)
    {
        var userId = Guid.NewGuid();
        await using var content = new MemoryStream([1, 2]);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var file = new UserFileUploadRequest
        {
            Content = content,
            FileName = "IMAGE.PNG",
            ContentType = "image/png",
            SizeInBytes = content.Length
        };

        var objectKey = isAvatar
            ? await _manager.UploadAvatarAsync(userId, file, cancellationToken)
            : await _manager.UploadBannerAsync(userId, file, cancellationToken);

        Assert.StartsWith($"users/{userId}/profile/{expectedKind}-", objectKey);
        Assert.EndsWith(".png", objectKey);
        await _objectStorageService.Received(1).UploadAsync(
            content,
            objectKey,
            file.ContentType,
            cancellationToken);
    }

    [Fact]
    public async Task TryDeleteAsync_SkipsEmptyAndDuplicateKeysAndContinuesAfterFailure()
    {
        _objectStorageService
            .DeleteAsync("broken-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("storage failed")));

        await _manager.TryDeleteAsync([null, "", "broken-key", "broken-key", "valid-key"]);

        await _objectStorageService.Received(1).DeleteAsync(
            "broken-key",
            Arg.Any<CancellationToken>());
        await _objectStorageService.Received(1).DeleteAsync(
            "valid-key",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetReplacedObjectKeys_ReturnsOnlyChangedOriginalKeys()
    {
        var result = ProfileImageManager.GetReplacedObjectKeys(
            "old-avatar",
            "same-banner",
            "new-avatar",
            "same-banner");

        Assert.Equal(["old-avatar"], result);
    }
}
