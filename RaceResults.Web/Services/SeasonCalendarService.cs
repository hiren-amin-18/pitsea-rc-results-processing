using Microsoft.EntityFrameworkCore;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public class SeasonCalendarService : ISeasonCalendarService
{
    private readonly IDbContextFactory<RaceResultsDbContext> _dbContextFactory;
    private readonly ILogger<SeasonCalendarService> _logger;

    public SeasonCalendarService(
        IDbContextFactory<RaceResultsDbContext> dbContextFactory,
        ILogger<SeasonCalendarService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public IReadOnlyList<SeasonFixturePreview> Preview(int year, SeasonCalendar.SeptemberOption septemberOption)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var existingDates = ExistingC2cDates(db, year);
        return SeasonCalendar.BuildFixtures(year, septemberOption)
            .Select(f => new SeasonFixturePreview(f, existingDates.Contains(f.EventDate.Date)))
            .ToList();
    }

    public SeasonCalendarResult Generate(int year, SeasonCalendar.SeptemberOption septemberOption)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var existingDates = ExistingC2cDates(db, year);
        var fixtures = SeasonCalendar.BuildFixtures(year, septemberOption);

        var created = 0;
        var skipped = new List<string>();
        foreach (var fixture in fixtures)
        {
            if (existingDates.Contains(fixture.EventDate.Date))
            {
                skipped.Add(fixture.EventDate.ToString("dd MMM yyyy"));
                continue;
            }

            db.Events.Add(new RaceEvent
            {
                EventName = fixture.EventName,
                EventDate = fixture.EventDate.Date,
                EventType = EventType.CrownToCrown,
                StartTime = fixture.StartTime,
                IsCurrent = false  // generation must not change the current selection (AC6)
            });
            created++;
        }

        if (created > 0)
        {
            db.SaveChanges();
            _logger.LogInformation("Season {Year}: generated {Count} Crown to Crown event(s); skipped {Skipped} existing date(s).",
                year, created, skipped.Count);
        }

        return new SeasonCalendarResult { CreatedCount = created, SkippedDates = skipped };
    }

    private static HashSet<DateTime> ExistingC2cDates(RaceResultsDbContext db, int year) =>
        db.Events
            .Where(e => e.EventType == EventType.CrownToCrown && e.EventDate.Year == year)
            .Select(e => e.EventDate)
            .ToList()
            .Select(d => d.Date)
            .ToHashSet();
}
