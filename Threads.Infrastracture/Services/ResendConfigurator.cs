using Microsoft.Extensions.Configuration;
using Resend;
using Threads.Infrastracture.Exceptions;

namespace Threads.Infrastracture.Services;

public static class ResendConfigurator
{
    public static void Configure(ResendClientOptions options, IConfiguration configuration)
    {
        var apiToken = configuration["RESEND_APITOKEN"] ??
                       throw new InfrastructureConfigurationException("RESEND_APITOKEN");

        options.ApiToken = apiToken;
        options.ThrowExceptions = true;
    }
}
