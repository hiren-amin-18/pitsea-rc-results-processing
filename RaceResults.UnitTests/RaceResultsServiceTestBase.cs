using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.Web.Services;
using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

/// <summary>
/// Base class that wires up an isolated RaceResultsService backed by in-memory SQLite.
/// Each test class that inherits this gets its own DB so tests are isolated.
/// </summary>
public abstract class RaceResultsServiceTestBase : IDisposable
{
    protected readonly RaceResultsService Service;
    private readonly SqliteConnection _connection;

    protected RaceResultsServiceTestBase()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _connection = connection;
        Service = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
