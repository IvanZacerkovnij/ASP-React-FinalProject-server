using Microsoft.AspNetCore.Http.Features;
using Threads.Api.ExceptionHandling;
using Threads.Infrastructure.Security;
using Threads.Infrastructure.Services;

namespace Threads.Api.Configuration;

public static class WebApplicationBuilderConfigurator
{
    public static WebApplicationBuilder AddApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        return builder;
    }

    public static WebApplicationBuilder AddUploadConfiguration(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options => UploadConfigurator.Configure(options));
        builder.Services.Configure<FormOptions>(options => UploadConfigurator.Configure(options));

        return builder;
    }

    public static WebApplicationBuilder AddGlobalExceptionHandler(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails(ProblemDetailsConfigurator.Configure);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        return builder;
    }

    public static WebApplicationBuilder AddForwardedHeaders(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
            ForwardedHeadersConfigurator.Configure(
                options,
                ProxyConfigurator.GetProxy(builder.Configuration)));

        return builder;
    }

    public static WebApplicationBuilder AddRateLimit(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(RateLimiterConfigurator.Configure);

        return builder;
    }
}
