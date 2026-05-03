using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceResults.Web.Data;

namespace RaceResults.UnitTests.Helpers;

/// <summary>
/// Creates an isolated in-memory SQLite DbContextFactory for each test.
/// Keeps a persistent connection open so the in-memory database survives across context instances.
/// </summary>
internal static class DbContextHelpers
{
    public static (IDbContextFactory<RaceResultsDbContext> Factory, SqliteConnection Connection) CreateFactory()
    {
        // Open a persistent connection — SQLite in-memory DB lives as long as this connection is open.
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddDbContextFactory<RaceResultsDbContext>(options =>
            options.UseSqlite(connection));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();

        // Create schema
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();

        return (factory, connection);
    }
}
