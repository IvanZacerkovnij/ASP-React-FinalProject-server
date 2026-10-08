using System.Net;

namespace Threads.Infrastructure.Services;

public static class LinkPreviewHttpConfigurator
{
    public static void Configure(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ThreadsLinkPreview/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
    }

    public static HttpMessageHandler CreatePrimaryHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.Brotli |
                                     DecompressionMethods.Deflate |
                                     DecompressionMethods.GZip,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            MaxResponseHeadersLength = 32,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
    }
}
