using Microsoft.Extensions.Configuration;
using Resend;

namespace Threads.Infrastracture.Services;

public static class ResendConfigurator
{
    public static void Configure(ResendClientOptions options, IConfiguration configuration)
    {
        options.ApiToken = configuration["RESEND_APITOKEN"]!;
    }
    
}