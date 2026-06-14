namespace RaceResults.Web.Models;

/// <summary>Season-wide dashboard across all events in a calendar year (US24).</summary>
public class SeasonDashboard
{
    public int Year { get; init; }
    public IReadOnlyList<int> AvailableYears { get; init; } = Array.Empty<int>();
    public IReadOnlyList<RaceEvent> Events { get; init; } = Array.Empty<RaceEvent>();
    public int TotalUniqueRunners { get; init; }

    public IReadOnlyList<AttendanceItem> MostAttendedRunners { get; init; } = Array.Empty<AttendanceItem>();
    public int EventCount { get; init; }

    public IReadOnlyList<ClubCount> TopClubsByEntries { get; init; } = Array.Empty<ClubCount>();
    public IReadOnlyList<ClubCount> TopClubsByRunners { get; init; } = Array.Empty<ClubCount>();

    public IReadOnlyList<CategoryFastest> FastestByCategory { get; init; } = Array.Empty<CategoryFastest>();
    public IReadOnlyList<MostImproved> MostImprovedByType { get; init; } = Array.Empty<MostImproved>();

    public IReadOnlyList<ParticipationRow> Participation { get; init; } = Array.Empty<ParticipationRow>();
    public double SeasonDnfRatePercent { get; init; }

    public int ExcludedTimeRows { get; init; }
}

public class AttendanceItem
{
    public required int RunnerId { get; init; }
    public required string RunnerName { get; init; }
    public string? Club { get; init; }
    public int EventsAttended { get; init; }
    public bool EverPresent { get; init; }
}

public class ClubCount
{
    public required string Club { get; init; }
    public int Count { get; init; }
}

public class CategoryFastest
{
    public required EventType EventType { get; init; }
    public required string Category { get; init; }
    public required string RunnerName { get; init; }
    public string? Club { get; init; }
    public required string Time { get; init; }
    public required string EventName { get; init; }
}

public class MostImproved
{
    public required EventType EventType { get; init; }
    public required string RunnerName { get; init; }
    public required string Improvement { get; init; } // e.g. "-1:23"
    public required string FirstTime { get; init; }
    public required string BestTime { get; init; }
}

public class ParticipationRow
{
    public required string EventName { get; init; }
    public DateTime EventDate { get; init; }
    public int Entrants { get; init; }
    public int Finishers { get; init; }
    public int FirstTimers { get; init; }
}

/// <summary>One runner's season across all events in a year (US24).</summary>
public class RunnerSeasonProfile
{
    public required Runner Runner { get; init; }
    public int Year { get; init; }
    public int EventsHeld { get; init; }
    public IReadOnlyList<RunnerRaceLine> Races { get; init; } = Array.Empty<RunnerRaceLine>();
    public IReadOnlyList<SeasonBest> SeasonBests { get; init; } = Array.Empty<SeasonBest>();
    public double? AverageFinishPosition { get; init; }
    public double? AverageCategoryPosition { get; init; }
    public IReadOnlyList<PointsProgressionPoint> ChampionsProgression { get; init; } = Array.Empty<PointsProgressionPoint>();
    public int CurrentStreak { get; init; }
    public int ExcludedTimeRows { get; init; }
}

public class RunnerRaceLine
{
    public required string EventName { get; init; }
    public DateTime EventDate { get; init; }
    public required EventType EventType { get; init; }
    public int Position { get; init; }
    public string? Time { get; init; }
    public required string Category { get; init; }
}

public class SeasonBest
{
    public required EventType EventType { get; init; }
    public required string Time { get; init; }
    public required string EventName { get; init; }
}

public class PointsProgressionPoint
{
    public required string EventName { get; init; }
    public DateTime EventDate { get; init; }
    public int PointsThisEvent { get; init; }
    public int CumulativePoints { get; init; }
}
