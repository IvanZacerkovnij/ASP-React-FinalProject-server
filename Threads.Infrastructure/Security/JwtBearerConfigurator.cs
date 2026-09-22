using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Threads.Infrastructure.Security;

public static class JwtBearerConfigurator
{
    public static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = jwtSection["Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var key = jwtSection["Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Threads.Security.Jwt");

                var logLevel = context.Exception is SecurityTokenInvalidSignatureException
                    ? LogLevel.Warning
                    : LogLevel.Debug;

                logger.Log(
                    logLevel,
                    "JWT authentication failed for {Method} {Endpoint} with {ExceptionType}. TraceId: {TraceId}",
                    context.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unknown",
                    context.Exception.GetType().Name,
                    context.HttpContext.TraceIdentifier);

                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Threads.Security.Jwt");
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

                logger.LogWarning(
                    "Authorization forbidden for {Method} {Endpoint}. TraceId: {TraceId}, UserId: {UserId}",
                    context.Request.Method,
                    context.HttpContext.GetEndpoint()?.DisplayName ?? "unknown",
                    context.HttpContext.TraceIdentifier,
                    userId);

                return Task.CompletedTask;
            }
        };
    }
}
