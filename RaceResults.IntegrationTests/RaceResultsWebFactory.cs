using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceResults.Web.Data;

namespace RaceResults.IntegrationTests;

/// <summary>
/// Replaces the SQLite DB with an isolated in-memory SQLite instance for each test run.
/// Holds a persistent connection so the schema survives across DbContext instances.
/// </summary>
public class RaceResultsWebFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly SqliteConnection _connection;

    public RaceResultsWebFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContextFactory registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDbContextFactory<RaceResultsDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            // Register factory that shares the persistent connection
            services.AddDbContextFactory<RaceResultsDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Creates the HttpClient and ensures the DB schema exists using the app's actual service provider.
    /// </summary>
    public new HttpClient CreateClient(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions options)
    {
        EnsureSchema();
        return base.CreateClient(options);
    }

    public new HttpClient CreateClient()
    {
        EnsureSchema();
        return base.CreateClient();
    }

    private bool _schemaCreated;

    private void EnsureSchema()
    {
        if (_schemaCreated) return;
        _schemaCreated = true;

        using var scope = Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
