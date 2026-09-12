using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Threads.Infrastructure.Services;

public static class RateLimiterConfigurator
{
    public const string VerificationPolicyName = "VerificationPolicy";
    public const string LoginPolicyName = "LoginPolicy";
    public const string ChangePasswordStartPolicyName = "ChangePasswordStartPolicy";
    public const string ChangePasswordConfirmPolicyName = "ChangePasswordConfirmPolicy";
    public const string RegisterPolicyName = "RegisterPolicy";
    public const string ForgotPasswordPolicyName = "ForgotPasswordPolicy";
    public const string ResendVerificationPolicyName = "ResendVerificationPolicy";
    public const string RefreshPolicyName = "RefreshPolicy";
    
    public const string PostCreationPolicyName = "PostCreationPolicy";
    public const string CommentCreationPolicyName = "CommentCreationPolicy";
    public const string InteractionPolicyName = "InteractionPolicy";
    public const string MediaUploadPolicyName = "MediaUploadPolicy";
    
    public const string ExternalSearchPolicyName = "ExternalSearchPolicy";
    
    public static void Configure(RateLimiterOptions options)
    {
        ConfigureResponse(options);
        
        AddPolicies(options);
    }

    private static void AddPolicies(RateLimiterOptions options)
    {
        options.AddPolicy(LoginPolicyName, httpContext =>
            FixedWindowByIp(httpContext, 5, TimeSpan.FromMinutes(1)));

        options.AddPolicy(RegisterPolicyName, httpContext =>
            FixedWindowByIp(httpContext, 3, TimeSpan.FromMinutes(15)));
        
        options.AddPolicy(ForgotPasswordPolicyName, httpContext =>
            FixedWindowByIp(httpContext, 3, TimeSpan.FromMinutes(15)));
        
        options.AddPolicy(ResendVerificationPolicyName, httpContext =>
            FixedWindowByIp(httpContext, 5, TimeSpan.FromMinutes(10)));
        
        options.AddPolicy(ChangePasswordStartPolicyName, httpContext =>
            FixedWindowByUser(httpContext, 3, TimeSpan.FromMinutes(15)));

        options.AddPolicy(ChangePasswordConfirmPolicyName, httpContext =>
            FixedWindowByUser(httpContext, 5, TimeSpan.FromMinutes(10)));
        
        options.AddPolicy(VerificationPolicyName, httpContext =>
            FixedWindowByIp(httpContext, 5, TimeSpan.FromMinutes(10)));

        options.AddPolicy(PostCreationPolicyName, httpContext =>
            TokenBucketByUser(httpContext, 5, 1, TimeSpan.FromSeconds(30)));

        options.AddPolicy(CommentCreationPolicyName, httpContext =>
            TokenBucketByUser(httpContext, 15, 5, TimeSpan.FromSeconds(30)));

        options.AddPolicy(InteractionPolicyName, httpContext =>
            TokenBucketByUser(httpContext, 60, 30, TimeSpan.FromSeconds(30)));

        options.AddPolicy(MediaUploadPolicyName, httpContext =>
            TokenBucketByUser(httpContext, 5, 1, TimeSpan.FromMinutes(1)));
        
        options.AddPolicy(ExternalSearchPolicyName, httpContext =>
            TokenBucketByIp(httpContext, 10, 5, TimeSpan.FromSeconds(10)));
        
        options.AddPolicy(RefreshPolicyName, httpContext =>
            FixedWindowByIp(httpContext, 20, TimeSpan.FromMinutes(1)));
    }

    private static void ConfigureResponse(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
            {
                context.HttpContext.Response.Headers["Retry-After"] =
                    Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
            }

            await context.HttpContext.Response.WriteAsJsonAsync(
                new
                {
                    message = "Too many requests. Please try again later."
                },
                cancellationToken);
        };
    }
    
    private static string GetUserKey(HttpContext context)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? context.Connection.RemoteIpAddress?.ToString()
               ?? "anonymous";
    }
    
    private static RateLimitPartition<string> FixedWindowByIp(
        HttpContext context,
        int permitLimit,
        TimeSpan window)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    private static RateLimitPartition<string> FixedWindowByUser(
        HttpContext context,
        int permitLimit,
        TimeSpan window)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            GetUserKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }

    private static RateLimitPartition<string> TokenBucketByUser(
        HttpContext context,
        int tokenLimit,
        int tokensPerPeriod,
        TimeSpan replenishmentPeriod)
    {
        return RateLimitPartition.GetTokenBucketLimiter(
            GetUserKey(context),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = tokenLimit,
                TokensPerPeriod = tokensPerPeriod,
                ReplenishmentPeriod = replenishmentPeriod,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }
    
    private static RateLimitPartition<string> TokenBucketByIp(
        HttpContext context,
        int tokenLimit,
        int tokensPerPeriod,
        TimeSpan replenishmentPeriod)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: ipAddress,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = tokenLimit,
                TokensPerPeriod = tokensPerPeriod,
                ReplenishmentPeriod = replenishmentPeriod,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    }
}
