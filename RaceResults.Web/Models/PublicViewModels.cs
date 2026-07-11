using RaceResults.Web.Services;

namespace RaceResults.Web.Models;

/// <summary>Read-only public results for a published event (US21).</summary>
public class PublicResultsViewModel
{
    public required RaceEvent Event { get; init; }
    public required IReadOnlyList<ResultRecord> Results { get; init; }
    public required IReadOnlyList<Entrant> DnfEntrants { get; init; }
    public ResultRecord? MaleWinner { get; init; }
    public ResultRecord? FemaleWinner { get; init; }
    public ResultRecord? MaleU18Winner { get; init; }
    public ResultRecord? FemaleU18Winner { get; init; }

    /// <summary>1st vet male (M40+) for Bluebell events (US33). Selected from the Vet Male Top 10 (no skip-top-3 here, since this is just the leading vet).</summary>
    public ResultRecord? MaleVetWinner { get; init; }
    public ResultRecord? FemaleVetWinner { get; init; }

    public int SeasonYear { get; init; }
}

/// <summary>Read-only public Champions leaderboard for a published event's season (US21).</summary>
public class PublicChampionsViewModel
{
    public required string Token { get; init; }
    public required string EventName { get; init; }
    public int SeasonYear { get; init; }
    public required IReadOnlyList<ChampionsLeaderboardEntry> Leaderboard { get; init; }

    /// <summary>When true, show the per-event breakdown (US44) instead of the summary.</summary>
    public bool ShowDetail { get; init; }

    /// <summary>The per-event breakdown, populated only when <see cref="ShowDetail"/> is true.</summary>
    public ChampionsDetail? Detail { get; init; }
}
