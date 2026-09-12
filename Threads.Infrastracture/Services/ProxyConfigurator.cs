using System.Net;
using Microsoft.Extensions.Configuration;
using Threads.Infrastracture.Exceptions;

namespace Threads.Infrastracture.Services;

public static class ProxyConfigurator
{
    public static IPAddress GetProxy(IConfiguration configurator)
    {
        var knownProxyValue = configurator["ReverseProxy:KnownProxy"];
        
        if (string.IsNullOrWhiteSpace(knownProxyValue))
        {
            throw new InfrastructureConfigurationException("ReverseProxy:KnownProxy is missing");
        }

        if (!IPAddress.TryParse(knownProxyValue, out var knownProxy))
        {
            throw new InfrastructureConfigurationException("ReverseProxy:KnownProxy must contain a valid IP address.");
        }

        return knownProxy;
    }
}
