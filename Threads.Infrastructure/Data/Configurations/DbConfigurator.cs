using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Threads.Infrastructure.Data;

namespace Threads.Infrastructure.Data.Configurations;

public static class DbConfigurator
{
    private const string ConnectionStringName = "DefaultConnection";

    public static void Configure(DbContextOptionsBuilder optionsBuilder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found.");
        }

        optionsBuilder.UseNpgsql(connectionString);
    }
}
