using Xunit;

namespace Threads.Infrastructure.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL integration tests";
}
