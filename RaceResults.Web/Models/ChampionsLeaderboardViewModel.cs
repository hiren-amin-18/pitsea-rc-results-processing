using RaceResults.Web.Services;

namespace RaceResults.Web.Models;

public class ChampionsLeaderboardViewModel
{
    public IReadOnlyList<ChampionsLeaderboardEntry> Leaderboard { get; set; } = new List<ChampionsLeaderboardEntry>();
    public int SeasonYear { get; set; }
    public int CurrentEventId { get; set; }
    public string CurrentEventName { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }

    /// <summary>When true, show the per-event breakdown (US44) instead of the summary.</summary>
    public bool ShowDetail { get; set; }

    /// <summary>The per-event breakdown, populated only when <see cref="ShowDetail"/> is true.</summary>
    public ChampionsDetail? Detail { get; set; }
}
