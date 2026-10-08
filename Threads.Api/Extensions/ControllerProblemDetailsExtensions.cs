using Microsoft.AspNetCore.Mvc;

namespace Threads.Api.Extensions;

public static class ControllerProblemDetailsExtensions
{
    public static ObjectResult ProblemResponse(
        this ControllerBase controller,
        int statusCode,
        string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = detail,
            Instance = controller.HttpContext.Request.Path
        };

        var result = new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
        result.ContentTypes.Add("application/problem+json");

        return result;
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Invalid request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status500InternalServerError => "Internal server error",
        _ => throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "Unsupported problem status code.")
    };
}
