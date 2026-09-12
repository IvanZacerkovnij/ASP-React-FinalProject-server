using Microsoft.Extensions.Configuration;
using Threads.Infrastructure.Exceptions;

namespace Threads.Infrastructure.Services;

public static class GifConfigurator
{
    public static void Configure(
        HttpClient client,
        IConfiguration configuration)
    {
        var url = configuration["GifApi:BaseURL"] ??
                  throw new InfrastructureConfigurationException("GifApi:BaseURL");

        client.BaseAddress = new Uri(url);
    }
}
