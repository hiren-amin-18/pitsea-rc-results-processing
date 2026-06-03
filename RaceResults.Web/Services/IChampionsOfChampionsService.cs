using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public interface IChampionsOfChampionsService
{
    /// <summary>Calculate and save points for the given event</summary>
    /// <param name="eventId">The Crown to Crown race event to score</param>
    /// <remarks>This runs after the event is completed and scores top 10 in each category</remarks>
    Task CalculateAndSaveEventPointsAsync(int eventId);

    /// <summary>Recalculate all points for a given season year (used when results are edited)</summary>
    /// <param name="seasonYear">The year to recalculate (e.g., 2026)</param>
    /// <remarks>This is called when any result in the season is edited to ensure leaderboard accuracy</remarks>
    Task RecalculateSeasonPointsAsync(int seasonYear);

    /// <summary>Get the cumulative Champions of Champions leaderboard for a given season as of a specific event</summary>
    /// <param name="seasonYear">The competition season (e.g., 2026)</param>
    /// <param name="asOfEventId">Optional: Only include events up to and including this event ID</param>
    /// <remarks>Results are ranked by total points, with ties broken by race count (descending)</remarks>
    Task<IReadOnlyList<ChampionsLeaderboardEntry>> GetLeaderboardAsync(int seasonYear, int? asOfEventId = null);

    /// <summary>Get current Champions leaderboard for the active season</summary>
    Task<IReadOnlyList<ChampionsLeaderboardEntry>> GetCurrentSeasonLeaderboardAsync(int? asOfEventId = null);

    /// <summary>Check if a runner is eligible for Champions points (must be in top 10 of their category)</summary>
    Task<bool> IsEligibleForPointsAsync(int entrantId, int eventId, string category);
}

/// <summary>Entry in the Champions of Champions leaderboard</summary>
public class ChampionsLeaderboardEntry
{
    /// <summary>Cumulative rank on leaderboard</summary>
    public int Rank { get; set; }

    /// <summary>The runner</summary>
    public required Entrant Entrant { get; set; }

    /// <summary>Category (Male, Female, Male U18, Female U18)</summary>
    public required string Category { get; set; }

    /// <summary>Total points accumulated this season</summary>
    public int TotalPoints { get; set; }

    /// <summary>Number of Crown to Crown races competed in</summary>
    public int RaceCount { get; set; }

    /// <summary>Indicates if this runner is tied on points with another runner (used for key indicator)</summary>
    public bool IsPointsTied { get; set; }

    /// <summary>Background color CSS class for display (gold, silver, bronze, or empty)</summary>
    public string? HighlightClass => Rank switch
    {
        1 => "rank-gold",
        2 => "rank-silver",
        3 => "rank-bronze",
        _ => null
    };
}
