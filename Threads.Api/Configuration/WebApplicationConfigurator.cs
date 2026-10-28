using Threads.Api.Middleware;

namespace Threads.Api.Configuration;

public static class WebApplicationConfigurator
{
    public static WebApplication ConfigureApplication(this WebApplication app)
    {
        app.MapOpenApi();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Threads API v1");
            options.RoutePrefix = "swagger";
        });

        app.UseForwardedHeaders();
        app.UseMiddleware<SlowRequestLoggingMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseCors("AllowAll");

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
