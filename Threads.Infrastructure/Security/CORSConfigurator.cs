using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Threads.Infrastructure.Security;

public static class CORSConfigurator
{
    private const string FrontendCorsPolicyName = "AllowAll";
    
    public static void Configure(CorsOptions options)
    {
        options.AddPolicy(FrontendCorsPolicyName, policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    }
}
