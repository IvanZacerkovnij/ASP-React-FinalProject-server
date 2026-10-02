using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;
using Threads.Infrastructure.Data;

namespace Threads.Infrastructure.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("threads_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ThreadsDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ThreadsDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .EnableDetailedErrors();

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        return new ThreadsDbContext(optionsBuilder.Options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var dbContext = CreateContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var dbContext = CreateContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "Users", "PendingRegistrations"
            RESTART IDENTITY CASCADE
            """);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
