namespace RaceResults.Web.Models;

/// <summary>Headline race statistics for the current event (US23): completion, gender split, finish-time summary.</summary>
public class RaceStatisticsSummary
{
    // Completion
    public int EntrantCount { get; init; }   // entrants who started (excludes DNS)
    public int FinisherCount { get; init; }
    public int DnfCount { get; init; }
    public int DnsCount { get; init; }
    public double CompletionRatePercent { get; init; }

    // Gender split (of finishers)
    public int MaleFinishers { get; init; }
    public int FemaleFinishers { get; init; }
    public double MalePercent { get; init; }
    public double FemalePercent { get; init; }

    // Affiliation (from RaceStats figures)
    public int AffiliatedCount { get; init; }
    public int UnaffiliatedCount { get; init; }

    // Finish times (typed durations)
    public bool HasTimes { get; init; }
    public TimeSpan? WinnerTime { get; init; }
    public TimeSpan? MedianTime { get; init; }
    public TimeSpan? AverageTime { get; init; }
    public TimeSpan? WinnerToMedianSpread { get; init; }
    public TimeSpan? Percentile25 { get; init; }
    public TimeSpan? Percentile50 { get; init; }
    public TimeSpan? Percentile75 { get; init; }

    /// <summary>Finishers whose time could not be parsed and were left out of the time stats (US23 AC5).</summary>
    public int ExcludedTimeRowCount { get; init; }

    // Busiest finish window
    public int? BusiestWindowMinute { get; init; }
    public int BusiestWindowCount { get; init; }
}
