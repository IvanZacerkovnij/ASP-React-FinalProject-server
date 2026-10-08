using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Threads.Application.Interfaces.ScheduledPosts;

namespace Threads.Infrastructure.Services;

public sealed class ScheduledPostPublishingWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledPostPublishingWorker> _logger;

    public ScheduledPostPublishingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledPostPublishingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IScheduledPostService>();
                var publishedCount = await service.PublishDueAsync(stoppingToken);

                if (publishedCount > 0)
                {
                    _logger.LogInformation(
                        "Published {PublishedCount} scheduled posts",
                        publishedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to publish scheduled posts");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
