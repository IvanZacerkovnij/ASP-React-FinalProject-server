using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.Exceptions;
using Threads.Infrastructure.Services;

namespace Threads.Infrastructure.IntegrationTests.Services;

public sealed class LinkPreviewServiceTests
{
    [Fact]
    public async Task ResolveAsync_WhenPageContainsOpenGraphMetadata_ReturnsPreview()
    {
        const string html = """
            <html>
              <head>
                <meta content="Article &amp; title" property="OG:TITLE">
                <meta property="og:image" content="/images/preview.jpg">
              </head>
            </html>
            """;
        using var httpClient = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        });
        var service = CreateService(httpClient);

        var result = await service.ResolveAsync("https://93.184.216.34/article");

        Assert.Equal("https://93.184.216.34/article", result.Url);
        Assert.Equal("93.184.216.34", result.Domain);
        Assert.Equal("Article & title", result.Title);
        Assert.Equal("https://93.184.216.34/images/preview.jpg", result.ImageUrl);
    }

    [Fact]
    public async Task ResolveAsync_WhenOpenGraphTitleIsMissing_UsesDocumentTitle()
    {
        using var httpClient = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "<html><head><title>  Fallback   title  </title></head></html>",
                Encoding.UTF8,
                "text/html")
        });
        var service = CreateService(httpClient);

        var result = await service.ResolveAsync("https://93.184.216.34/article");

        Assert.Equal("Fallback title", result.Title);
    }

    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://10.0.0.1/internal")]
    [InlineData("file:///tmp/page.html")]
    public async Task ResolveAsync_WhenUrlIsNotPublicHttp_RejectsRequest(string url)
    {
        using var httpClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP request must not be sent."));
        var service = CreateService(httpClient);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.ResolveAsync(url));
    }

    private static LinkPreviewService CreateService(HttpClient httpClient)
    {
        return new LinkPreviewService(
            httpClient,
            Substitute.For<ILogger<LinkPreviewService>>());
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        return new HttpClient(new StubHttpMessageHandler(responseFactory));
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
