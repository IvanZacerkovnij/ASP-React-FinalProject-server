using Microsoft.AspNetCore.Mvc;
using Threads.Api.Responses;

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
        StatusCodes.Status400BadRequest => ApiErrorTitles.InvalidRequest,
        StatusCodes.Status401Unauthorized => ApiErrorTitles.Unauthorized,
        StatusCodes.Status403Forbidden => ApiErrorTitles.Forbidden,
        StatusCodes.Status404NotFound => ApiErrorTitles.ResourceNotFound,
        StatusCodes.Status409Conflict => ApiErrorTitles.Conflict,
        StatusCodes.Status500InternalServerError => ApiErrorTitles.InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "Unsupported problem status code.")
    };
}
