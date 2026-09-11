using Microsoft.Extensions.Configuration;

namespace Threads.Infrastracture.Services;

public static class LocationConfigurator
{
    public static void Configure(
        HttpClient client,
        IConfiguration configuration)
    {
        string url = configuration["LocationApi:BaseURL"] ??
                     throw new InvalidOperationException("You must set LocationUrl in appsettings.json");
        client.BaseAddress = new Uri(url);
    }
}