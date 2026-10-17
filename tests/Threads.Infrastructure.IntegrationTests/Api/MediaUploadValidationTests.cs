using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Threads.Api.Requests.Validation;
using Threads.Infrastructure.Services;

namespace Threads.Infrastructure.IntegrationTests.Api;

public sealed class MediaUploadValidationTests
{
    [Theory]
    [InlineData("image/jpeg", 50 * 1024 * 1024, true)]
    [InlineData("image/png", 50 * 1024 * 1024, true)]
    [InlineData("image/webp", 50 * 1024 * 1024, true)]
    [InlineData("image/gif", 50 * 1024 * 1024, true)]
    [InlineData("image/jpeg", 50 * 1024 * 1024 + 1, false)]
    [InlineData("video/mp4", 256 * 1024 * 1024, true)]
    [InlineData("video/quicktime", 256 * 1024 * 1024, true)]
    [InlineData("video/webm", 256 * 1024 * 1024, true)]
    [InlineData("video/mp4", 256 * 1024 * 1024 + 1, false)]
    public void MediaFile_ValidatesSizeBoundary(string contentType, long sizeInBytes, bool expected)
    {
        using var content = new MemoryStream();
        var file = new FormFile(content, 0, sizeInBytes, "file", "media")
        {
            Headers = new HeaderDictionary(), ContentType = contentType
        };

        Assert.Equal(expected, new MediaUploadFileAttribute().IsValid(file));
    }

    [Theory]
    [InlineData(50 * 1024 * 1024, true)]
    [InlineData(50 * 1024 * 1024 + 1, false)]
    public void ProfileImage_ValidatesSizeBoundary(long sizeInBytes, bool expected)
    {
        using var content = new MemoryStream();
        var file = new FormFile(content, 0, sizeInBytes, "avatar", "avatar.png")
        {
            Headers = new HeaderDictionary(), ContentType = "image/png"
        };

        Assert.Equal(expected, new ProfileImageFileAttribute().IsValid(file));
    }

    [Fact]
    public void UploadLimits_AllowMaximumVideoWithMultipartOverhead()
    {
        var kestrelOptions = new KestrelServerOptions();
        var formOptions = new FormOptions();

        UploadConfigurator.Configure(kestrelOptions);
        UploadConfigurator.Configure(formOptions);

        Assert.Equal(256 * 1024 * 1024, formOptions.MultipartBodyLengthLimit);
        Assert.Equal(257 * 1024 * 1024, kestrelOptions.Limits.MaxRequestBodySize);
    }
}
