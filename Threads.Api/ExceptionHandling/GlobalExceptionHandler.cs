using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Threads.Application.Exceptions;
using Threads.Infrastracture.Exceptions;

namespace Threads.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            RequestValidationException =>
                (StatusCodes.Status400BadRequest,
                    "Invalid request",
                    exception.Message),
            
            NotFoundException =>
                (StatusCodes.Status404NotFound,
                    "Resource not found",
                    exception.Message),
            
            ConflictException =>
                (StatusCodes.Status409Conflict,
                    "Conflict",
                    exception.Message),
            
            ForbiddenException =>
                (StatusCodes.Status403Forbidden,
                    "Forbidden",
                    exception.Message),

            MediaProcessingException =>
                (StatusCodes.Status500InternalServerError,
                    "Media processing failed",
                    "Unable to process the media file"),

            ExternalServiceException =>
                (StatusCodes.Status502BadGateway,
                    "External service error",
                    "An external service is unavailable"),

            InfrastructureConfigurationException =>
                (StatusCodes.Status500InternalServerError,
                    "Application configuration error",
                    "The application is temporarily unavailable"),

            _ =>
                (StatusCodes.Status500InternalServerError,
                    "Internal server error",
                    "An unexpected error occurred")
        };
        
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
}
