using Microsoft.Extensions.Configuration;

namespace Threads.Infrastracture.Services;

public static class GifConfigurator
{
    public static void Configure(
        HttpClient client,
        IConfiguration configuration)
    {
        string url = configuration["GifApi:BaseURL"] ??
                     throw new InvalidOperationException("You must set GifUrl in appsettings.json");
        client.BaseAddress = new Uri(url);
    }
}