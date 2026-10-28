using Threads.Api.Configuration;

namespace Threads.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddUploadConfiguration();
        builder.AddDbConfiguration();

        builder.AddRepositories();
        builder.AddApplicationServices();
        builder.AddInfrastructureServices();
        builder.AddSecurityServices();

        builder.AddExternalApi();

        builder.AddCache();
        builder.AddGlobalExceptionHandler();
        builder.AddForwardedHeaders();
        builder.AddRateLimit();

        builder.AddApi();

        var app = builder.Build();

        app.ConfigureApplication();

        app.Run();
    }
}
