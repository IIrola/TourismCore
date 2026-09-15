using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Tourism.Infrastructure.Persistence;

/// <summary>
/// Builds a <see cref="TourismDbContext"/> for the EF tooling: <c>dotnet ef</c>, and the migration bundle
/// the container image carries.
///
/// It exists because of what EF does without one. Finding no factory, the tooling constructs the
/// application host to get at the context — which runs <c>Program</c>, startup work included. In
/// the bundle that meant the schema could never be created: the startup code queries tables the
/// migrations had not created yet, so it failed against exactly the empty database a migrator is
/// pointed at. A design-time factory takes precedence over that fallback, so the tooling gets a
/// context and nothing else runs.
///
/// Only the schema is its business — no seeding, no DI container, no secrets.
/// </summary>
public sealed class TourismDbContextFactory : IDesignTimeDbContextFactory<TourismDbContext>
{
    private const string ConnectionStringName = "DefaultConnection";

    public TourismDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TourismDbContext>()
            .UseMySql(ResolveConnectionString(), DatabaseServer.Version)
            .Options;

        return new TourismDbContext(options);
    }

    /// <summary>
    /// Configuration only, and in the order a deployment actually supplies it: the environment
    /// first, so a container needs nothing but <c>ConnectionStrings__DefaultConnection</c>, then
    /// the startup project's appsettings, which is what a developer running <c>dotnet ef</c> has.
    /// The bundle's own <c>--connection</c> argument overrides whatever comes out of here.
    /// </summary>
    private static string ResolveConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json",
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        // Designing a migration needs a provider, not a reachable server, so an unusable value
        // beats throwing: `migrations add` keeps working on a machine with no database at all.
        // Anything that does touch a server is given a real string by the caller.
        return "Server=design-time-placeholder;Database=tourism;User=none;Password=none;";
    }
}
