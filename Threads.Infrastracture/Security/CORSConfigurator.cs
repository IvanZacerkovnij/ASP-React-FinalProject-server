using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Threads.Infrastracture.Security;

public static  class CORSConfigurator
{
    private const string FrontendCorsPolicyName = "AllowAll";
    
    public static void Configure(CorsOptions options, IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        
        options.AddPolicy(FrontendCorsPolicyName, policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    }
}