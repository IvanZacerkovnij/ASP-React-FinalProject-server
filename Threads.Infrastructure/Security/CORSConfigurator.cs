using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Threads.Infrastructure.Security;

public static class CORSConfigurator
{
    private const string FrontendCorsPolicyName = "AllowAll";
    
    public static void Configure(CorsOptions options, IConfiguration configuration)
    {
        options.AddPolicy(FrontendCorsPolicyName, policy =>
        {
            
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    }
}
