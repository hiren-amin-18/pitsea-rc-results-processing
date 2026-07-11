namespace RaceResults.Web.Models;

/// <summary>
/// C2C-only, season-to-date statistics for a calendar year, anchored to the current event.
/// Complements the per-event Race Stats page with cross-event trends (participation, retention,
/// finish-time progression, attendance, and the Champions points race).
/// </summary>
public class C2CSeasonStats
{
    public int Year { get; init; }
    public IReadOnlyList<int> AvailableYears { get; init; } = Array.Empty<int>();

    /// <summary>True when there is at least one C2C event in the season up to the current event.</summary>
    public bool HasData { get; init; }

    // --- Progress ("Event N of M") ---
    public string CurrentEventName { get; init; } = string.Empty;

    /// <summary>C2C events run so far this season (up to and including the current event).</summary>
    public int EventsRun { get; init; }

    /// <summary>Total C2C events scheduled in the season (including any not yet run).</summary>
    public int EventsScheduled { get; init; }

    // --- Season-to-date KPI cards ---
    public int UniqueRunners { get; init; }
    public int TotalFinishers { get; init; }
    public double AverageFieldSize { get; init; }
    public double SeasonDnfRatePercent { get; init; }

    /// <summary>Runners present at every C2C event run so far this season.</summary>
    public int EverPresentCount { get; init; }

    /// <summary>Per-event trend series, chronological, up to and including the current event.</summary>
    public IReadOnlyList<SeasonEventPoint> Events { get; init; } = Array.Empty<SeasonEventPoint>();

    /// <summary>How many runners attended exactly 1, 2, 3… events (core-vs-casual shape).</summary>
    public IReadOnlyList<AttendanceBucket> AttendanceDistribution { get; init; } = Array.Empty<AttendanceBucket>();

    /// <summary>Top clubs by unique runners across the season-to-date.</summary>
    public IReadOnlyList<ClubCount> TopClubs { get; init; } = Array.Empty<ClubCount>();

    // --- Champions points race (May–Sept window only) ---
    /// <summary>True when at least one in-window event has awarded points.</summary>
    public bool HasPointsData { get; init; }

    /// <summary>Event names for the points-race x-axis (in-window C2C events, chronological).</summary>
    public IReadOnlyList<string> PointsRaceEventNames { get; init; } = Array.Empty<string>();

    /// <summary>Top contenders' cumulative points aligned to <see cref="PointsRaceEventNames"/>.</summary>
    public IReadOnlyList<PointsRaceRunner> PointsRace { get; init; } = Array.Empty<PointsRaceRunner>();

    /// <summary>Finisher rows dropped from time-based trends because the time was unparseable.</summary>
    public int ExcludedTimeRows { get; init; }
}

/// <summary>One C2C event's contribution to the season trend series.</summary>
public class SeasonEventPoint
{
    public required string EventName { get; init; }
    public DateTime EventDate { get; init; }
    public int Entrants { get; init; }        // starters (excludes DNS)
    public int Finishers { get; init; }
    public int FirstTimers { get; init; }     // first C2C appearance of the season
    public int ReturningRunners { get; init; }
    public int MaleFinishers { get; init; }
    public int FemaleFinishers { get; init; }
    public int CumulativeUniqueRunners { get; init; }
    public double DnfRatePercent { get; init; }

    /// <summary>Winning finish time in whole seconds; null when the event has no timed finishes.</summary>
    public int? WinnerSeconds { get; init; }

    /// <summary>Median finish time in whole seconds; null when the event has no timed finishes.</summary>
    public int? MedianSeconds { get; init; }
}

/// <summary>Runners who attended exactly <see cref="Events"/> C2C events this season.</summary>
public class AttendanceBucket
{
    public int Events { get; init; }
    public int Runners { get; init; }
}

/// <summary>One contender's cumulative Champions points across the in-window events.</summary>
public class PointsRaceRunner
{
    public required string RunnerName { get; init; }

    /// <summary>Cumulative points after each in-window event (aligned to the event x-axis).</summary>
    public IReadOnlyList<int> CumulativePoints { get; init; } = Array.Empty<int>();

    public int Total { get; init; }
}
