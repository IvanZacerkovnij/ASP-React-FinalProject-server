using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Threads.Infrastracture.Data;

namespace Threads.Infrastracture.Data.Configurations;

public static class DbConfigurator
{
    private const string ConnectionStringName = "DefaultConnection";

    public static void Configure(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();


        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found.");
        }

        optionsBuilder.UseNpgsql(connectionString);
    }
    
}
