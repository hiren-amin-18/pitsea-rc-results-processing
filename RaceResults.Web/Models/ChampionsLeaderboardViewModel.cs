using RaceResults.Web.Services;

namespace RaceResults.Web.Models;

public class ChampionsLeaderboardViewModel
{
    public IReadOnlyList<ChampionsLeaderboardEntry> Leaderboard { get; set; } = new List<ChampionsLeaderboardEntry>();
    public int SeasonYear { get; set; }
    public int CurrentEventId { get; set; }
    public string CurrentEventName { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
}
