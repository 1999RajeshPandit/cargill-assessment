using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crr.Api.Data;

public sealed class RiskPositionDbContextFactory : IDesignTimeDbContextFactory<RiskPositionDbContext>
{
    public RiskPositionDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddUserSecrets<RiskPositionDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("RiskPositions");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:RiskPositions in appsettings.json, .NET user-secrets, or the ConnectionStrings__RiskPositions environment variable.");

        var options = new DbContextOptionsBuilder<RiskPositionDbContext>().UseNpgsql(connectionString).Options;
        return new RiskPositionDbContext(options);
    }
}
