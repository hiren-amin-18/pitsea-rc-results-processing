using RaceResults.Web.Services;

namespace RaceResults.Web.Models;

/// <summary>End-of-season review (US30): the year's full story across results, Champions, and recognition.</summary>
public class SeasonReview
{
    public int Year { get; init; }
    public required IReadOnlyList<int> AvailableYears { get; init; }
    public required SeasonHeadlines Headlines { get; init; }

    /// <summary>Year-on-year deltas — only populated when the previous season has data (US30 AC5).</summary>
    public SeasonComparison? Comparison { get; init; }

    /// <summary>Champions of Champions final standings (US14).</summary>
    public required IReadOnlyList<ChampionsLeaderboardEntry> ChampionsLeaderboard { get; init; }
    public int ChampionsDistinctScorers { get; init; }
    public int ChampionsScoringEventCount { get; init; }

    /// <summary>Cross-event runner recognition from US24.</summary>
    public required SeasonDashboard SeasonDashboard { get; init; }

    /// <summary>Course records set during this year (US22). Empty when none.</summary>
    public required IReadOnlyList<CourseRecord> NewCourseRecordsThisYear { get; init; }

    /// <summary>DNF / DSQ summary across the year (US16). Empty zeros are still meaningful here.</summary>
    public int TotalDnf { get; init; }
    public int TotalDsq { get; init; }

    /// <summary>The configurable awards list (US30 §Awards List). Single source of calculation.</summary>
    public required AwardsList Awards { get; init; }

    // --- Volunteer recognition (US29 wired up). When no volunteer assignments exist in the year,
    // VolunteerRecognition is null and the view/PDF omit the section gracefully.
    public VolunteerRecognition? VolunteerRecognition { get; init; }
    public bool HasVolunteerData => VolunteerRecognition is not null;
}

public class VolunteerRecognition
{
    public int TotalInstances { get; init; }
    public int UniqueVolunteers { get; init; }
    public int EventsCovered { get; init; }
    public int TotalBallotEntries { get; init; }

    /// <summary>Top contributors by events attended (ties broken by assignments, then name).</summary>
    public required IReadOnlyList<VolunteerSeasonProfile> MostActive { get; init; }

    /// <summary>Volunteers who attended every event of the year.</summary>
    public required IReadOnlyList<VolunteerSeasonProfile> EverPresent { get; init; }

    /// <summary>Volunteers who both ran and volunteered in the same year (recognises double commitment).</summary>
    public required IReadOnlyList<VolunteerSeasonProfile> RanAndVolunteered { get; init; }
}

public class SeasonHeadlines
{
    public int EventsHeld { get; init; }
    public int TotalEntrants { get; init; }
    public int TotalFinishers { get; init; }
    public int TotalUniqueRunners { get; init; }
    public double CompletionRatePercent { get; init; }
}

public class SeasonComparison
{
    public required SeasonHeadlines Previous { get; init; }
    public int EntrantDelta { get; init; }
    public double EntrantPercentChange { get; init; }
    public int UniqueRunnerDelta { get; init; }
    public int EventsHeldDelta { get; init; }
}

public class AwardsList
{
    /// <summary>One winner per Champions category — Male, Female, Male U18, Female U18.</summary>
    public required IReadOnlyList<AwardEntry> ChampionsWinners { get; init; }
    public required IReadOnlyList<AwardEntry> EverPresentRunners { get; init; }
    public AwardEntry? MostImprovedRunner { get; init; }

    /// <summary>Volunteer of the season — most active volunteer when at least one assignment exists in the year.</summary>
    public AwardEntry? VolunteerOfTheSeason { get; init; }

    /// <summary>Ever-present volunteers, mirroring the runner ever-present award.</summary>
    public IReadOnlyList<AwardEntry> EverPresentVolunteers { get; init; } = Array.Empty<AwardEntry>();
}

public class AwardEntry
{
    public required string Title { get; init; }
    public required string Winner { get; init; }
    public string? Detail { get; init; }
}
