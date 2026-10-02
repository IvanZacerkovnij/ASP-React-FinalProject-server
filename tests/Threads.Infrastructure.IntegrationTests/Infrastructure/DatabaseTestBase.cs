namespace Threads.Infrastructure.IntegrationTests.Infrastructure;

public abstract class DatabaseTestBase : IAsyncLifetime
{
    protected DatabaseTestBase(PostgreSqlFixture fixture)
    {
        Fixture = fixture;
    }

    protected PostgreSqlFixture Fixture { get; }

    public Task InitializeAsync()
    {
        return Fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
