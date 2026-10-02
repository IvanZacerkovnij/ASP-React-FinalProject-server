using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Threads.Application.Interfaces.Auth;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Api;

internal sealed class ThreadsApiFactory : WebApplicationFactory<Threads.Api.Program>
{
    private readonly PostgreSqlFixture _database;

    public ThreadsApiFactory(PostgreSqlFixture database)
    {
        _database = database;
        EmailService = new TestAuthEmailService();
    }

    public TestAuthEmailService EmailService { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _database.ConnectionString,
                ["Jwt:Issuer"] = "threads-api-tests",
                ["Jwt:Audience"] = "threads-api-client",
                ["Jwt:Key"] = "threads-api-integration-test-signing-key-32-bytes-minimum",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["AuthCodes:HashKey"] = Convert.ToBase64String(
                    Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
                ["Redis:ConnectionString"] = "localhost:1,abortConnect=false",
                ["RESEND_APITOKEN"] = "test-token",
                ["RESEND_FROM_EMAIL"] = "tests@example.com",
                ["AWS:S3:Region"] = "eu-central-1",
                ["AWS:S3:BucketName"] = "test-bucket",
                ["GIPHY_API_KEY"] = "test-key",
                ["GEOAPIFY_API_KEY"] = "test-key"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthEmailService>();
            services.AddSingleton<IAuthEmailService>(EmailService);
            services.RemoveAll<HybridCache>();
            services.AddSingleton<HybridCache, ApiTestHybridCache>();
        });
    }
}
