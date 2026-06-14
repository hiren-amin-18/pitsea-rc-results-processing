using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class DatabasePathResolverTests
{
    [Fact]
    public void Resolve_ExplicitConnectionStringWins()
    {
        var configured = "Data Source=C:\\custom\\path\\raceresults.db";
        Assert.Equal(configured, DatabasePathResolver.Resolve(configured));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_FallsBackToPerUserPath_WhenUnset(string? configured)
    {
        var connectionString = DatabasePathResolver.Resolve(configured);

        Assert.Contains("PitseaRaceResults", connectionString);
        Assert.Contains("raceresults.db", connectionString);
        Assert.StartsWith("Data Source=", connectionString);
    }

    [Fact]
    public void GetDefaultDataDirectory_PointsAtAppFolder()
    {
        var dir = DatabasePathResolver.GetDefaultDataDirectory();
        Assert.False(string.IsNullOrEmpty(dir));
        Assert.EndsWith("PitseaRaceResults", dir);
    }
}
