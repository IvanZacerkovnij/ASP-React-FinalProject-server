using System.Diagnostics;
using System.Security.Claims;

namespace Threads.Api.Middleware;

public sealed class SlowRequestLoggingMiddleware
{
    private const int DefaultThresholdMilliseconds = 1500;

    private readonly RequestDelegate _next;
    private readonly ILogger<SlowRequestLoggingMiddleware> _logger;
    private readonly int _thresholdMilliseconds;

    public SlowRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<SlowRequestLoggingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        var configuredThreshold = configuration.GetValue<int?>(
            "Logging:SlowRequestThresholdMilliseconds");
        _thresholdMilliseconds = configuredThreshold is > 0
            ? configuredThreshold.Value
            : DefaultThresholdMilliseconds;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        if (context.RequestAborted.IsCancellationRequested ||
            stopwatch.ElapsedMilliseconds < _thresholdMilliseconds)
        {
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var endpointName = context.GetEndpoint()?.DisplayName
                           ?? context.Request.Path.Value
                           ?? "unknown";

        _logger.LogWarning(
            "Slow request {Method} {Endpoint} completed with status code {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}, UserId: {UserId}",
            context.Request.Method,
            endpointName,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            context.TraceIdentifier,
            userId);
    }
}
