namespace RaceResults.Web.Models;

public class ResultsPageViewModel
{
    public List<ResultRecord> Results { get; set; } = new();
    public List<Entrant> DnfEntrants { get; set; } = new();
}
