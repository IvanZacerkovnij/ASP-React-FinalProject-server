using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Threads.Infrastructure.Services;

public static class ForwardedHeadersConfigurator
{
    public static void Configure(ForwardedHeadersOptions options, IPAddress proxy)
    {
        options.ForwardedHeaders =
             ForwardedHeaders.XForwardedFor |
             ForwardedHeaders.XForwardedProto;
        
        options.KnownProxies.Add(proxy);
    }
}
