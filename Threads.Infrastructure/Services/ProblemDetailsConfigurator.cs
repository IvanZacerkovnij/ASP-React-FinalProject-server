using Microsoft.AspNetCore.Http;

namespace Threads.Infrastructure.Services;

public static class ProblemDetailsConfigurator
{
    public static void Configure(ProblemDetailsOptions options)
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        };
    }
}
