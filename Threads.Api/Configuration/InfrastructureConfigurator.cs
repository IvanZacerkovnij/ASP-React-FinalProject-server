using Resend;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Gifs;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Locations;
using Threads.Application.Interfaces.LinkPreviews;
using Threads.Infrastructure.Data;
using Threads.Infrastructure.Data.Configurations;
using Threads.Infrastructure.Services;

namespace Threads.Api.Configuration;

public static class InfrastructureConfigurator
{
    public static WebApplicationBuilder AddInfrastructureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IMediaProcessingService, FfmpegMediaProcessingService>();
        builder.Services.AddScoped<IObjectStorageService, S3ObjectStorageService>();
        builder.Services.AddScoped<IAuthEmailService, AuthEmailService>();
        builder.Services.AddHostedService<ScheduledPostPublishingWorker>();
        builder.Services.AddResend(options => ResendConfigurator.Configure(options, builder.Configuration));

        return builder;
    }

    public static WebApplicationBuilder AddExternalApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<IGifSearchService, GiphyGifSearchService>(client => GifConfigurator.Configure(client, builder.Configuration));
        builder.Services.AddHttpClient<ILocationSearchService, GeoapifyLocationSearchService>(client => LocationConfigurator.Configure(client, builder.Configuration));
        builder.Services
            .AddHttpClient<ILinkPreviewService, LinkPreviewService>(LinkPreviewHttpConfigurator.Configure)
            .ConfigurePrimaryHttpMessageHandler(LinkPreviewHttpConfigurator.CreatePrimaryHandler);

        return builder;
    }

    public static WebApplicationBuilder AddCache(this WebApplicationBuilder builder)
    {
        builder.Services.AddStackExchangeRedisCache(options => RedisConfigurator.Configure(options, builder.Configuration));
        builder.Services.AddHybridCache(HybridCacheConfigurator.Configure);

        return builder;
    }

    public static WebApplicationBuilder AddDbConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<ThreadsDbContext>(options => DbConfigurator.Configure(options, builder.Configuration));

        return builder;
    }
}
