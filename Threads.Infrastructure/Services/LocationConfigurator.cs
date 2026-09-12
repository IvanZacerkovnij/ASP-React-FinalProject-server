using Microsoft.Extensions.Configuration;
using Threads.Infrastructure.Exceptions;

namespace Threads.Infrastructure.Services;

public static class LocationConfigurator
{
    public static void Configure(
        HttpClient client,
        IConfiguration configuration)
    {
        var url = configuration["LocationApi:BaseURL"] ??
                  throw new InfrastructureConfigurationException("LocationApi:BaseURL");

        client.BaseAddress = new Uri(url);
    }
}
