using System.Text.Json;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Services.LinkPreviews;

namespace Threads.Application.UnitTests.Services.LinkPreviews;

public sealed class LinkPreviewResponseFactoryTests
{
    [Fact]
    public void Create_NormalizesUrlAndBuildsStableResponse()
    {
        var first = LinkPreviewResponseFactory.Create(
            " https://Example.com/article ",
            " Article title ",
            "/images/preview.jpg");
        var second = LinkPreviewResponseFactory.Create(
            "https://example.com/article",
            "Article title",
            "https://example.com/images/preview.jpg");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("https://example.com/article", first.Url);
        Assert.Equal("example.com", first.Domain);
        Assert.Equal("Article title", first.Title);
        Assert.Equal("https://example.com/images/preview.jpg", first.ImageUrl);
    }

    [Fact]
    public void Create_WhenUrlIsNotHttp_ThrowsRequestValidationException()
    {
        Assert.Throws<RequestValidationException>(() =>
            LinkPreviewResponseFactory.Create("file:///tmp/page.html", null, null));
    }

    [Fact]
    public void UpdatePostRequest_DeserializesRemovalFlags()
    {
        var request = JsonSerializer.Deserialize<UpdatePostRequest>(
            """
            {
              "removePoll": true,
              "removeLocation": true,
              "removeLinkPreview": true
            }
            """,
            WebOptions());

        Assert.NotNull(request);
        Assert.True(request.RemovePoll);
        Assert.True(request.RemoveLocation);
        Assert.True(request.RemoveLinkPreview);
    }

    private static JsonSerializerOptions WebOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
}
