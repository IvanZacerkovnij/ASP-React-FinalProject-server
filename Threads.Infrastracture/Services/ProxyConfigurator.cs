using System.Net;
using Microsoft.Extensions.Configuration;
using Threads.Infrastracture.Exceptions;

namespace Threads.Infrastracture.Services;

public static class ProxyConfigurator
{
    public static IPAddress GetProxy(IConfiguration configuration)
    {
        const string configurationKey = "ReverseProxy:KnownProxy";
        var knownProxyValue = configuration[configurationKey] ?? "127.0.0.1";
        
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
