using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarvedRock.Data;

/// <summary>
/// Creates the context for EF Core commands without starting the web application.
/// </summary>
public class LocalContextFactory : IDesignTimeDbContextFactory<LocalContext>
{
    public LocalContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CARVEDROCK_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set CARVEDROCK_CONNECTION before running an EF Core command.");

        var options = new DbContextOptionsBuilder<LocalContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new LocalContext(options);
    }
}
