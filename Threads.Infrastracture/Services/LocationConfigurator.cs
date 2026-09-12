using Microsoft.Extensions.Configuration;
using Threads.Infrastracture.Exceptions;

namespace Threads.Infrastracture.Services;

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
