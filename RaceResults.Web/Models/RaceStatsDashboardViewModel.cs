namespace RaceResults.Web.Models;

public class RaceStatsDashboardViewModel
{
    public RaceStats Stats { get; set; } = new();
    public List<BreakdownItem> ClubBreakdown { get; set; } = new();
    public List<BreakdownItem> FinishersPerMinute { get; set; } = new();
}

public class BreakdownItem
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}
