using System.Net;
using Microsoft.Extensions.Configuration;
using Threads.Infrastructure.Exceptions;

namespace Threads.Infrastructure.Services;

public static class ProxyConfigurator
{
    public static IPAddress GetProxy(IConfiguration configuration)
    {
        const string configurationKey = "ReverseProxy:KnownProxy";
        var knownProxyValue = configuration[configurationKey];
        
        if (string.IsNullOrWhiteSpace(knownProxyValue))
        {
            throw new InfrastructureConfigurationException(configurationKey);
        }

        if (!IPAddress.TryParse(knownProxyValue, out var knownProxy))
        {
            throw new InvalidOperationException(
                $"Configuration key '{configurationKey}' must contain a valid IP address.");
        }

        return knownProxy;
    }
}
