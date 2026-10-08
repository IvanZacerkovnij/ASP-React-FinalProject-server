using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Threads.Application.Exceptions;
using Threads.Api.Exceptions;
using Threads.Api.Responses;
using Threads.Infrastructure.Exceptions;

namespace Threads.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request {Method} {Endpoint} was canceled by the client. TraceId: {TraceId}",
                httpContext.Request.Method,
                GetEndpointName(httpContext),
                httpContext.TraceIdentifier);
            return true;
        }

        var (statusCode, title, detail) = exception switch
        {
            InvalidUserClaimsException =>
                (StatusCodes.Status401Unauthorized,
                    ApiErrorTitles.Unauthorized,
                    exception.Message),

            RequestValidationException =>
                (StatusCodes.Status400BadRequest,
                    ApiErrorTitles.InvalidRequest,
                    exception.Message),

            NotFoundException =>
                (StatusCodes.Status404NotFound,
                    ApiErrorTitles.ResourceNotFound,
                    exception.Message),

            ConflictException =>
                (StatusCodes.Status409Conflict,
                    ApiErrorTitles.Conflict,
                    exception.Message),

            ForbiddenException =>
                (StatusCodes.Status403Forbidden,
                    ApiErrorTitles.Forbidden,
                    exception.Message),

            MediaProcessingException =>
                (StatusCodes.Status500InternalServerError,
                    ApiErrorTitles.MediaProcessingFailed,
                    "Unable to process the media file"),

            ExternalServiceException =>
                (StatusCodes.Status502BadGateway,
                    ApiErrorTitles.ExternalServiceError,
                    "An external service is unavailable"),

            InfrastructureConfigurationException =>
                (StatusCodes.Status500InternalServerError,
                    ApiErrorTitles.ApplicationConfigurationError,
                    "The application is temporarily unavailable"),

            _ =>
                (StatusCodes.Status500InternalServerError,
                    ApiErrorTitles.InternalServerError,
                    "An unexpected error occurred")
        };

        LogException(httpContext, exception, statusCode);

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Instance = httpContext.Request.Path,
                },
                Exception = exception
            });
    }

    private void LogException(HttpContext httpContext, Exception exception, int statusCode)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var endpointName = GetEndpointName(httpContext);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Request {Method} {Endpoint} failed with status code {StatusCode}. TraceId: {TraceId}, UserId: {UserId}",
                httpContext.Request.Method,
                endpointName,
                statusCode,
                httpContext.TraceIdentifier,
                userId);
            return;
        }

        if (exception is ConflictException or ForbiddenException)
        {
            logger.LogWarning(
                "Request {Method} {Endpoint} was rejected with status code {StatusCode} and exception {ExceptionType}. TraceId: {TraceId}, UserId: {UserId}",
                httpContext.Request.Method,
                endpointName,
                statusCode,
                exception.GetType().Name,
                httpContext.TraceIdentifier,
                userId);
            return;
        }

        logger.LogDebug(
            "Request {Method} {Endpoint} completed with status code {StatusCode} and exception {ExceptionType}. TraceId: {TraceId}, UserId: {UserId}",
            httpContext.Request.Method,
            endpointName,
            statusCode,
            exception.GetType().Name,
            httpContext.TraceIdentifier,
            userId);
    }

    private static string GetEndpointName(HttpContext httpContext)
    {
        return httpContext.GetEndpoint()?.DisplayName
               ?? httpContext.Request.Path.Value
               ?? "unknown";
    }
}
