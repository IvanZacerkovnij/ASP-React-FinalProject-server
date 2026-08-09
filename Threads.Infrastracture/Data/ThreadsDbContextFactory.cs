using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Threads.Infrastracture.Data;

public class ThreadsDbContextFactory : IDesignTimeDbContextFactory<ThreadsDbContext>
{
    private const string ConnectionStringName = "DefaultConnection";
    
    public ThreadsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Threads.Api"))
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found.");
        }
        
        var optionsBuilder = new DbContextOptionsBuilder<ThreadsDbContext>();
        
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseAdminDatabase("defaultdb"));
        return new ThreadsDbContext(optionsBuilder.Options);
    }
}