namespace RaceResults.Web.Models;

public class RaceStatsDashboardViewModel
{
    public RaceStats Stats { get; set; } = new();
    public RaceStatisticsSummary Summary { get; set; } = new();
    public List<BreakdownItem> ClubBreakdown { get; set; } = new();
    public List<BreakdownItem> FinishersPerMinute { get; set; } = new();

    /// <summary>True when the event is Bluebell 5 (US33). Drives the U18/Vet split swap on the stats page.</summary>
    public bool IsBluebell { get; set; }
}

public class BreakdownItem
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}
