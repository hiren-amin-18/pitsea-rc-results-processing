using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class SeasonCalendarTests
{
    // Known Good Friday dates verified against the standard Easter algorithm / publicly available tables.
    [Theory]
    [InlineData(2024, 3, 29)] // Easter Sun 2024-03-31 — earliest-Easter edge case
    [InlineData(2025, 4, 18)]
    [InlineData(2026, 4, 3)]
    [InlineData(2027, 3, 26)] // Easter Sun 2027-03-28 — March-Easter edge
    [InlineData(2030, 4, 19)]
    [InlineData(2038, 4, 23)] // Easter Sun 2038-04-25 — latest-Easter edge case
    public void GoodFriday_MatchesKnownDates(int year, int expectedMonth, int expectedDay)
    {
        var date = SeasonCalendar.GoodFriday(year);
        Assert.Equal(new DateTime(year, expectedMonth, expectedDay), date);
    }

    [Fact]
    public void SecondWednesday_FallsOnWednesday()
    {
        for (var year = 2024; year <= 2030; year++)
        {
            for (var month = 5; month <= 9; month++)
            {
                var date = SeasonCalendar.SecondWednesday(year, month);
                Assert.Equal(DayOfWeek.Wednesday, date.DayOfWeek);
                Assert.InRange(date.Day, 8, 14);
            }
        }
    }

    [Fact]
    public void BuildFixtures_HasSevenFixtures_InCalendarOrder()
    {
        var fixtures = SeasonCalendar.BuildFixtures(2026, SeasonCalendar.SeptemberOption.SecondWednesday);
        Assert.Equal(7, fixtures.Count);
        for (var i = 1; i < fixtures.Count; i++)
        {
            Assert.True(fixtures[i].EventDate > fixtures[i - 1].EventDate);
        }
        Assert.Equal(new TimeSpan(11, 0, 0), fixtures.First().StartTime);
        Assert.Equal(new TimeSpan(11, 0, 0), fixtures.Last().StartTime); // Boxing Day
        Assert.Equal(new DateTime(2026, 12, 26), fixtures.Last().EventDate);
    }
}

public class SeasonCalendarServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly SeasonCalendarService _service;
    private readonly RaceResultsService _raceService;

    public SeasonCalendarServiceTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _connection = connection;
        _raceService = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
        _service = new SeasonCalendarService(factory, NullLogger<SeasonCalendarService>.Instance);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Generate_CreatesAllFixtures_AndDoesNotChangeCurrentEvent()
    {
        var currentBefore = _raceService.GetCurrentEvent().Id;

        var result = _service.Generate(2027, SeasonCalendar.SeptemberOption.SecondWednesday);

        // 2027's Good Friday is 26 March 2027 (different from the seeded 3 April 2026 event),
        // so all 7 fixtures should be created.
        Assert.Equal(7, result.CreatedCount);
        Assert.Empty(result.SkippedDates);
        Assert.Equal(currentBefore, _raceService.GetCurrentEvent().Id);

        using var db = _factory.CreateDbContext();
        Assert.Equal(7, db.Events.Count(e => e.EventType == EventType.CrownToCrown && e.EventDate.Year == 2027));
    }

    [Fact]
    public void Generate_IdempotentReRun_IsNoOp()
    {
        _service.Generate(2027, SeasonCalendar.SeptemberOption.SecondWednesday);
        var second = _service.Generate(2027, SeasonCalendar.SeptemberOption.SecondWednesday);

        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(7, second.SkippedDates.Count);
    }

    [Fact]
    public void Preview_FlagsExistingDate()
    {
        // The seeded default event is on Good Friday 2026 (3 April). Preview 2026 → that fixture is flagged.
        var preview = _service.Preview(2026, SeasonCalendar.SeptemberOption.SecondWednesday);
        var goodFriday = preview.Single(p => p.Fixture.EventDate == new DateTime(2026, 4, 3));
        Assert.True(goodFriday.AlreadyExists);
    }
}
