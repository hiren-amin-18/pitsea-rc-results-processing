using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Generates the Crown to Crown season fixtures from the club's fixed date rules (US31).</summary>
public interface ISeasonCalendarService
{
    /// <summary>Preview the year's fixtures, flagging any whose date already has a C2C event so the organiser can review before generating.</summary>
    IReadOnlyList<SeasonFixturePreview> Preview(int year, SeasonCalendar.SeptemberOption septemberOption);

    /// <summary>Generate the missing C2C events for the year. Idempotent and non-destructive: existing events are never modified.</summary>
    SeasonCalendarResult Generate(int year, SeasonCalendar.SeptemberOption septemberOption);
}

/// <summary>A previewed fixture plus whether a C2C event already exists on that date (US31 AC4).</summary>
public record SeasonFixturePreview(SeasonFixture Fixture, bool AlreadyExists);

public class SeasonCalendarResult
{
    public required int CreatedCount { get; init; }
    public required IReadOnlyList<string> SkippedDates { get; init; }
}
