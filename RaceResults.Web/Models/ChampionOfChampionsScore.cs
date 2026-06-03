namespace RaceResults.Web.Models;

public class ChampionOfChampionsScore
{
    public int Id { get; set; }
    
    /// <summary>The year of the Champions of Champions season (e.g., 2026)</summary>
    public int SeasonYear { get; set; }
    
    /// <summary>The runner (Entrant) who earned these points</summary>
    public int EntrantId { get; set; }
    public Entrant? Entrant { get; set; }
    
    /// <summary>The category for which points were earned (Male, Female, Male U18, Female U18)</summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>Cumulative points earned across all Crown to Crown races in the season</summary>
    public int TotalPoints { get; set; }
    
    /// <summary>Number of Crown to Crown races this runner participated in during the season</summary>
    public int RaceCount { get; set; }
    
    /// <summary>Last updated timestamp</summary>
    public DateTime LastUpdated { get; set; }
}
